-- OrderFlow v1.0. Solo para una base de datos de desarrollo.
DROP TABLE IF EXISTS order_items CASCADE;
DROP TABLE IF EXISTS orders CASCADE;
DROP TABLE IF EXISTS products CASCADE;
DROP TABLE IF EXISTS dining_tables CASCADE;

CREATE TABLE dining_tables (
  id BIGSERIAL PRIMARY KEY,
  table_number INTEGER NOT NULL UNIQUE CHECK (table_number > 0),
  capacity INTEGER NOT NULL DEFAULT 4 CHECK (capacity > 0),
  status VARCHAR(20) NOT NULL DEFAULT 'Available'
    CHECK (status IN ('Available','Occupied','Reserved'))
);
CREATE TABLE products (
  id BIGSERIAL PRIMARY KEY,
  name VARCHAR(120) NOT NULL,
  description VARCHAR(500),
  price NUMERIC(10,2) NOT NULL CHECK (price >= 0),
  category VARCHAR(60) NOT NULL DEFAULT 'General',
  is_available BOOLEAN NOT NULL DEFAULT TRUE
);
CREATE TABLE orders (
  id BIGSERIAL PRIMARY KEY,
  table_id BIGINT NOT NULL REFERENCES dining_tables(id),
  status VARCHAR(20) NOT NULL DEFAULT 'Pending'
    CHECK (status IN ('Pending','Preparing','Ready','Delivered','Cancelled')),
  notes VARCHAR(500),
  total NUMERIC(10,2) NOT NULL DEFAULT 0,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE TABLE order_items (
  id BIGSERIAL PRIMARY KEY,
  order_id BIGINT NOT NULL REFERENCES orders(id) ON DELETE CASCADE,
  product_id BIGINT NOT NULL REFERENCES products(id),
  product_name VARCHAR(120) NOT NULL,
  quantity INTEGER NOT NULL CHECK (quantity > 0),
  unit_price NUMERIC(10,2) NOT NULL,
  subtotal NUMERIC(10,2) NOT NULL
);
CREATE INDEX ix_orders_status ON orders(status);
INSERT INTO dining_tables(table_number,capacity) VALUES (1,4),(2,2),(3,6),(4,4);
INSERT INTO products(name,description,price,category) VALUES
('Hamburguesa clásica','Carne, queso, lechuga y jitomate',89,'Comida'),
('Papas a la francesa','Porción mediana',45,'Acompañamiento'),
('Agua de limón','Vaso de 500 ml',28,'Bebida'),
('Ensalada de la casa','Vegetales frescos',72,'Comida');
