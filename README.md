# OrderFlow — v1.0 inicial

MVP académico para el sistema de comandas de ByteCompany.

## Tecnologías
- App móvil: Flutter + Dart
- Backend: ASP.NET Core 8 Web API (C#)
- Base de datos: PostgreSQL
- Panel web: HTML, CSS y JavaScript
- Versionado: Git + GitHub

## Alcance de esta versión
- API REST: productos, mesas y comandas.
- PostgreSQL con datos de demostración.
- Panel web para consultar comandas, mesas y productos.
- App Flutter inicial para consultar comandas.

## Preparación
1. Crear una base de datos PostgreSQL llamada `orderflow`.
2. Ejecutar `database/init.sql`.
3. Configurar la variable `ConnectionStrings__Default`.
4. Ejecutar el backend:
   ```bash
   cd backend/OrderFlow.Api
   dotnet restore
   dotnet run --urls "http://0.0.0.0:5080"
   ```
   PowerShell:
   ```powershell
   $env:ConnectionStrings__Default="Host=localhost;Port=5432;Database=orderflow;Username=postgres;Password=TU_PASSWORD"
   ```
5. Abrir `web/index.html` o servir la carpeta con `python -m http.server 5500`.
6. Generar los archivos de plataforma Flutter y ejecutar la app:
   ```bash
   cd mobile/orderflow_app
   flutter create .
   flutter pub get
   flutter run
   ```
   El ZIP incluye el código Dart y `pubspec.yaml`; `flutter create .` genera las carpetas nativas Android/Windows necesarias en el equipo de desarrollo.

En `web/app.js`, `API_BASE` debe apuntar a la URL de la API. En `mobile/orderflow_app/lib/config.dart`, ajustar la URL: emulador Android `10.0.2.2`, teléfono físico la IP local de la PC.

## Endpoints
- `GET /api/health`
- `GET, POST /api/products`
- `GET, POST /api/tables`
- `GET, POST /api/orders`
- `PATCH /api/orders/{id}/status`

Estados: `Pending`, `Preparing`, `Ready`, `Delivered`, `Cancelled`.

## Importante
Es una base de desarrollo, no un sistema listo para producción. Aún faltan autenticación segura, permisos reales, caja, pruebas automatizadas, despliegue, HTTPS y respaldos. No subir contraseñas ni cadenas de conexión al repositorio. El script SQL reinicia las tablas del esquema `public`: usar solamente en una base de datos de desarrollo.
