using Npgsql;
using System.Data;

var builder = WebApplication.CreateBuilder(args);
var cs = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
    ?? builder.Configuration.GetConnectionString("Default")
    ?? "Host=localhost;Port=5432;Database=orderflow;Username=postgres;Password=postgres";

builder.Services.AddSingleton(NpgsqlDataSource.Create(cs));
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseCors();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", app = "OrderFlow", version = "1.0.0" }));

app.MapGet("/api/products", async (NpgsqlDataSource db) =>
{
    await using var cmd = db.CreateCommand("SELECT id,name,description,price,category,is_available FROM products WHERE is_available=TRUE ORDER BY name");
    await using var r = await cmd.ExecuteReaderAsync();
    var data = new List<object>();
    while (await r.ReadAsync())
        data.Add(new { id=r.GetInt64(0), name=r.GetString(1), description=r.IsDBNull(2)?null:r.GetString(2),
            price=r.GetDecimal(3), category=r.GetString(4), isAvailable=r.GetBoolean(5) });
    return Results.Ok(data);
});

app.MapPost("/api/products", async (ProductInput x, NpgsqlDataSource db) =>
{
    if (string.IsNullOrWhiteSpace(x.Name) || x.Price < 0) return Results.BadRequest(new { message="Nombre y precio válido requeridos." });
    await using var cmd = db.CreateCommand("INSERT INTO products(name,description,price,category) VALUES(@n,@d,@p,@c) RETURNING id");
    cmd.Parameters.AddWithValue("n",x.Name.Trim());
    cmd.Parameters.AddWithValue("d",(object?)x.Description ?? DBNull.Value);
    cmd.Parameters.AddWithValue("p",x.Price);
    cmd.Parameters.AddWithValue("c",string.IsNullOrWhiteSpace(x.Category)?"General":x.Category);
    var id=(long)(await cmd.ExecuteScalarAsync())!;
    return Results.Created($"/api/products/{id}",new { id });
});

app.MapGet("/api/tables", async (NpgsqlDataSource db) =>
{
    await using var cmd=db.CreateCommand("SELECT id,table_number,capacity,status FROM dining_tables ORDER BY table_number");
    await using var r=await cmd.ExecuteReaderAsync();
    var data=new List<object>();
    while(await r.ReadAsync()) data.Add(new { id=r.GetInt64(0),tableNumber=r.GetInt32(1),capacity=r.GetInt32(2),status=r.GetString(3) });
    return Results.Ok(data);
});

app.MapPost("/api/tables", async (TableInput x,NpgsqlDataSource db) =>
{
    if(x.TableNumber<=0 || x.Capacity<=0) return Results.BadRequest(new { message="Número y capacidad deben ser mayores que cero." });
    await using var cmd=db.CreateCommand("INSERT INTO dining_tables(table_number,capacity) VALUES(@n,@c) RETURNING id");
    cmd.Parameters.AddWithValue("n",x.TableNumber); cmd.Parameters.AddWithValue("c",x.Capacity);
    try { var id=(long)(await cmd.ExecuteScalarAsync())!; return Results.Created($"/api/tables/{id}",new { id }); }
    catch(PostgresException e) when(e.SqlState=="23505") { return Results.Conflict(new { message="El número de mesa ya existe." }); }
});

app.MapGet("/api/orders", async (NpgsqlDataSource db) =>
{
    await using var cmd=db.CreateCommand("""
      SELECT o.id,o.table_id,t.table_number,o.status,o.notes,o.total,o.created_at,
      COALESCE(json_agg(json_build_object('productId',i.product_id,'productName',i.product_name,
      'quantity',i.quantity,'unitPrice',i.unit_price,'subtotal',i.subtotal)) FILTER(WHERE i.id IS NOT NULL),'[]'::json)
      FROM orders o JOIN dining_tables t ON t.id=o.table_id
      LEFT JOIN order_items i ON i.order_id=o.id
      GROUP BY o.id,t.table_number ORDER BY o.created_at DESC
      """);
    await using var r=await cmd.ExecuteReaderAsync();
    var data=new List<object>();
    while(await r.ReadAsync()) data.Add(new { id=r.GetInt64(0),tableId=r.GetInt64(1),tableNumber=r.GetInt32(2),
      status=r.GetString(3),notes=r.IsDBNull(4)?null:r.GetString(4),total=r.GetDecimal(5),
      createdAt=r.GetDateTime(6),items=System.Text.Json.JsonDocument.Parse(r.GetString(7)).RootElement });
    return Results.Ok(data);
});

app.MapPost("/api/orders", async (OrderInput x,NpgsqlDataSource db) =>
{
    if(x.TableId<=0 || x.Items is null || x.Items.Count==0 || x.Items.Any(i=>i.ProductId<=0 || i.Quantity<=0))
        return Results.BadRequest(new { message="Selecciona una mesa y al menos un producto con cantidad válida." });
    await using var cn=await db.OpenConnectionAsync();
    await using var tx=await cn.BeginTransactionAsync();
    try
    {
        await using(var check=new NpgsqlCommand("SELECT status FROM dining_tables WHERE id=@id FOR UPDATE",cn,tx))
        {
            check.Parameters.AddWithValue("id",x.TableId);
            var s=await check.ExecuteScalarAsync();
            if(s is null) return Results.NotFound(new { message="Mesa no encontrada." });
            if((string)s!="Available") return Results.Conflict(new { message="La mesa no está disponible." });
        }
        long orderId;
        await using(var cmd=new NpgsqlCommand("INSERT INTO orders(table_id,notes) VALUES(@t,@n) RETURNING id",cn,tx))
        {
            cmd.Parameters.AddWithValue("t",x.TableId); cmd.Parameters.AddWithValue("n",(object?)x.Notes??DBNull.Value);
            orderId=(long)(await cmd.ExecuteScalarAsync())!;
        }
        decimal total=0;
        foreach(var item in x.Items)
        {
            string name; decimal price;
            await using(var p=new NpgsqlCommand("SELECT name,price FROM products WHERE id=@id AND is_available=TRUE",cn,tx))
            {
                p.Parameters.AddWithValue("id",item.ProductId);
                await using var pr=await p.ExecuteReaderAsync();
                if(!await pr.ReadAsync()) return Results.BadRequest(new { message=$"Producto {item.ProductId} no disponible." });
                name=pr.GetString(0); price=pr.GetDecimal(1);
            }
            var sub=price*item.Quantity; total+=sub;
            await using var line=new NpgsqlCommand("INSERT INTO order_items(order_id,product_id,product_name,quantity,unit_price,subtotal) VALUES(@o,@p,@n,@q,@u,@s)",cn,tx);
            line.Parameters.AddWithValue("o",orderId); line.Parameters.AddWithValue("p",item.ProductId);
            line.Parameters.AddWithValue("n",name); line.Parameters.AddWithValue("q",item.Quantity);
            line.Parameters.AddWithValue("u",price); line.Parameters.AddWithValue("s",sub);
            await line.ExecuteNonQueryAsync();
        }
        await using(var upd=new NpgsqlCommand("UPDATE orders SET total=@v WHERE id=@id; UPDATE dining_tables SET status='Occupied' WHERE id=@t",cn,tx))
        { upd.Parameters.AddWithValue("v",total); upd.Parameters.AddWithValue("id",orderId); upd.Parameters.AddWithValue("t",x.TableId); await upd.ExecuteNonQueryAsync(); }
        await tx.CommitAsync();
        return Results.Created($"/api/orders/{orderId}",new { id=orderId,total });
    }
    catch { await tx.RollbackAsync(); throw; }
});

app.MapPatch("/api/orders/{id:long}/status", async (long id,StatusInput x,NpgsqlDataSource db) =>
{
    var allowed=new[]{"Pending","Preparing","Ready","Delivered","Cancelled"};
    if(!allowed.Contains(x.Status)) return Results.BadRequest(new { message="Estado no válido." });
    await using var cn=await db.OpenConnectionAsync();
    await using var tx=await cn.BeginTransactionAsync();
    await using var cmd=new NpgsqlCommand("UPDATE orders SET status=@s,updated_at=NOW() WHERE id=@id RETURNING table_id",cn,tx);
    cmd.Parameters.AddWithValue("s",x.Status); cmd.Parameters.AddWithValue("id",id);
    var table=await cmd.ExecuteScalarAsync();
    if(table is null) return Results.NotFound(new { message="Comanda no encontrada." });
    if(x.Status is "Delivered" or "Cancelled")
    {
        await using var free=new NpgsqlCommand("UPDATE dining_tables SET status='Available' WHERE id=@t",cn,tx);
        free.Parameters.AddWithValue("t",(long)table); await free.ExecuteNonQueryAsync();
    }
    await tx.CommitAsync();
    return Results.Ok(new { id,status=x.Status });
});
app.Run();

record ProductInput(string Name,string? Description,decimal Price,string? Category);
record TableInput(int TableNumber,int Capacity);
record ItemInput(long ProductId,int Quantity);
record OrderInput(long TableId,string? Notes,List<ItemInput> Items);
record StatusInput(string Status);
