# Plan de implementación

Basado en [GLOSSARY.md](../GLOSSARY.md) y los ADRs en [docs/adr/](adr/). Cada fase termina en algo funcionando y desplegable. Las fases 2 y 3 se pueden hacer en paralelo una vez terminada la fase 1.

**Entorno verificado:** Node 24.15, npm 11.12, .NET SDK 10.0.401, Docker 26, Angular CLI 22.2.4 (vía `npx`).

## Fase 0: Fundaciones

- Conectar este directorio a `maedrome/spreadsheet-templates` y conservar `GLOSSARY.md` y `docs/`.
- Estructura del monorepo:
  ```
  frontend/          Angular 22 (standalone, zoneless, signals, Tailwind v4, Vitest)
  backend/
    src/Api/         ASP.NET Core 10, Minimal APIs, carpetas por feature: Catalogo/, Compras/, Admin/
    tests/Api.Tests/ xUnit + Testcontainers (PostgreSQL)
  docker-compose.yml        producción (Dokploy)
  docker-compose.dev.yml    solo PostgreSQL para desarrollo local
  ```
- `frontend/Dockerfile`: build multi-stage con `nginx:alpine`, `try_files $uri /index.html` y `Cache-Control: immutable` para los assets con hash.
- `backend/Dockerfile`: imagen `aspnet:10.0`, usuario no root, volumen `/data/files`.
- La API aplica las migraciones de EF Core al arrancar y expone `/api/health`.
- GitHub Actions: build y tests de frontend y backend en cada PR.

**Listo cuando:** con `docker compose up`, el placeholder de Angular se ve en `/` y `/api/health` responde 200.

## Fase 1: Modelo de dominio y persistencia

- Entidades (EF Core + Npgsql):
  - `Template`: slug, nombre, descripción, precio en centavos (USD), estado Publicado, posición, ruta del `.xlsx`, link `/copy` de Google Sheets
  - `Preview`: pertenece a una Plantilla, orden (la primera es la Preview principal), variantes WebP por tamaño
  - `Bundle` + `BundleTemplate` (muchos a muchos con orden): slug, nombre, descripción, precio, estado Publicado, posición
  - `Order`: email, Producto comprado (Plantilla o Paquete), precio pagado, id de la orden en Polar (único, para idempotencia), estado (`Paid`/`Refunded`), fecha
  - `OrderTemplate`: las Plantillas incluidas en el momento de pagar, como snapshot
  - `PurchaseLink`: hash SHA-256 del token, `OrderId`, fecha. Una Compra puede tener varios: `/recuperar` emite uno nuevo sin invalidar los anteriores, y la base de datos nunca guarda tokens en claro
- Reglas de dominio, con tests:
  - Una Plantilla se puede publicar solo si tiene nombre, descripción, precio > 0, al menos una Preview, el `.xlsx` y el link de Google Sheets
  - Un Paquete se puede publicar solo si tiene al menos 2 Plantillas publicadas y un Ahorro mayor que 0
  - El slug no cambia una vez publicado
  - Una Plantilla con Compras no se puede borrar
  - No se puede despublicar una Plantilla mientras esté en un Paquete publicado
  - El Ahorro se calcula a partir de los precios actuales

**Listo cuando:** las migraciones aplican contra un Postgres de Testcontainers y las reglas tienen tests.

## Fase 2: Catálogo público

- API (anónima): `GET /api/catalogo` (home), `GET /api/plantillas/{slug}`, `GET /api/paquetes/{slug}`, `GET /api/previews/{archivo}` (caché immutable), `GET /sitemap.xml` y `GET /robots.txt` (disallow `/admin`, `/compra`).
- Angular:
  - Home: hero, Paquetes, grilla de Plantillas, preguntas frecuentes, footer
  - Detalle de Plantilla y de Paquete, con galería de Previews y Ahorro
  - Imágenes responsive (`srcset` con las variantes WebP, `NgOptimizedImage`)
  - `Title` y `Meta` por ruta (ADR 0002)
  - Página 404
- Datos de seed para desarrollo (3 Plantillas, 1 Paquete).

**Listo cuando:** el catálogo de seed se navega bien en 375 px y en desktop, y Lighthouse mobile da SEO ≥ 95.

## Fase 3: Panel de administración

- Autenticación: ASP.NET Core Identity con un solo usuario, cookie HttpOnly `SameSite=Strict` y 2FA TOTP obligatorio. El admin se crea con el comando `dotnet Api.dll create-admin`. Rate limiting en el login.
- Rutas `/admin/**` lazy-loaded, protegidas por un guard. No aparecen enlazadas desde el sitio.
- CRUD de Plantillas: textos, precio, subida y orden de Previews (conversión a WebP en 3 tamaños, validando tipo y peso), subida del `.xlsx` (validando la extensión, la firma ZIP y el tamaño), y validación del link `docs.google.com/.../copy`.
- CRUD de Paquetes y orden de aparición en la home. Publicar y despublicar aplicando las reglas de la fase 1, con errores claros.
- Lista de Compras (email, Producto, monto, fecha, estado) con un botón para reenviar el Enlace de compra.

**Listo cuando:** puedes crear una Plantilla desde cero, publicarla y verla en la home.

## Fase 4: Checkout y Compras (Polar)

- **Spike primero:** confirmar en la documentación de Polar si un checkout requiere un producto creado en Polar o acepta un precio ad hoc. Si requiere producto, publicar en el admin crea o actualiza el producto en Polar y guarda su id. Pedir la aprobación de la cuenta de Polar **ya**, porque puede tardar.
- `IPaymentProvider` con la implementación `PolarPaymentProvider` (ADR 0003). Desarrollo con el sandbox de Polar.
- `POST /api/checkout` {tipo, slug} crea la sesión y devuelve la URL de Polar. La `success_url` es `/compra/confirmando?checkout={id}`.
- `POST /api/webhooks/polar`:
  - Verifica la firma (Standard Webhooks)
  - Con `order.paid`, crea la Compra de forma idempotente, guarda el snapshot de Plantillas, genera el Enlace de compra y envía el email
  - Con `order.refunded`, marca la Compra como Reembolsada
- La página "Confirmando tu pago..." consulta `GET /api/checkout/{id}/estado` hasta que exista la Compra y luego redirige a `/compra/:token`.
- `IEmailSender` con la implementación de Resend, y una plantilla de email en español.

**Listo cuando:** una compra en el sandbox termina en el email y en la página de descarga, y un webhook repetido no duplica la Compra.

## Fase 5: Descargas y recuperación

- `/compra/:token`: lista las Plantillas de la Compra con la descarga del `.xlsx` y el link de Google Sheets. Si la Compra está Reembolsada, muestra un mensaje en lugar de las descargas.
- `GET /api/compras/{token}/plantillas/{id}/xlsx` devuelve el archivo vigente con `Content-Disposition: attachment`. Responde 404 si el token es inválido y 410 si la Compra está Reembolsada.
- `/recuperar`: un formulario que siempre muestra el mismo mensaje y envía un email con los links de todas las Compras de ese email. Rate limiting de 3 por hora por IP y por email.

**Listo cuando:** se puede descargar con un enlace viejo, se recuperan las compras por email y una Compra reembolsada no se puede descargar.

## Fase 6: Contenido legal

- `/terminos` (incluye la política de reembolso de 14 días y la mención de Polar como Merchant of Record), `/privacidad` (LOPDP: datos que se recogen, uso solo para entregar la compra, derechos del titular) y textos finales de las preguntas frecuentes.
- Los redacto en borrador y tú los revisas.

## Fase 7: Despliegue en Hetzner

- VPS (CX22 o CX32), Dokploy, y una aplicación de tipo compose: `web` (nginx), `api` y `postgres`, con volúmenes para Postgres y para `/data/files`.
- Traefik: un solo dominio apuntando a `web`, con HTTPS de Let's Encrypt. nginx reenvía `/api/` a `api`, así que el enrutamiento es igual en local y en producción. En la fase 2 se agregan a nginx las rutas `/sitemap.xml` y `/robots.txt` hacia `api`.
- Secretos en Dokploy: cadena de conexión, token y secreto del webhook de Polar, API key de Resend.
- Backup diario de Postgres y del volumen de archivos a Hetzner Object Storage, con retención de 30 días. Probar una restauración una vez.
- Umami desde la plantilla de Dokploy.
- Dominio, DNS y SPF/DKIM de Resend. Configurar la URL del webhook de producción en Polar.
- **Go-live:** una compra real de $1, un reembolso, una recuperación y un backup restaurado.

## Fase 8: Diseño visual

- Iterar con Claude Design después de la fase 2. Primero hay que arreglar el login (`/design-login`).
- Llevar el resultado a los tokens de Tailwind (`@theme`) y a los componentes.

## Después del lanzamiento

Tests E2E con Playwright, PayPal, newsletter con consentimiento, e inyección de metas desde .NET si las previews en redes sociales llegan a importar (ADR 0002).

## Riesgos

| Riesgo | Mitigación |
|---|---|
| Polar rechaza o demora la cuenta | Pedirla en la fase 0. Lemon Squeezy queda como plan B detrás de `IPaymentProvider` |
| El modelo de productos de Polar obliga a sincronizar el catálogo | Spike al inicio de la fase 4 |
| La licencia de la librería de imágenes | Usar NetVips o SkiaSharp. ImageSharp solo si se cumplen los términos de su licencia |
| Emails de Resend que caen en spam | Dominio propio con SPF, DKIM y DMARC antes del go-live |
