# Tienda de plantillas de presupuesto

Tienda online donde se venden plantillas de presupuesto (Excel y Google Sheets) de forma individual o agrupadas en paquetes, a clientes que compran sin crear cuenta.

## Catálogo

**Producto** (`Product`):
Cualquier cosa que se puede comprar en la tienda: una Plantilla o un Paquete.
_Evitar_: Ítem, artículo

**Plantilla** (`Template`):
Una hoja de cálculo de presupuesto que se vende individualmente, con su propio precio. Se entrega como archivo Excel y como versión de Google Sheets.
_Evitar_: Spreadsheet, budget spreadsheet, hoja, planilla

**Paquete** (`Bundle`):
Un Producto que agrupa varias Plantillas elegidas por el vendedor, con un precio propio y fijo. Una Plantilla puede formar parte de varios Paquetes.
_Evitar_: Pack, combo, kit

**Ahorro** (`Savings`):
Diferencia entre la suma de los precios actuales de las Plantillas de un Paquete y el precio del Paquete.
_Evitar_: Descuento, rebaja

**Publicado** (`Published`):
Estado de un Producto que es visible y comprable en la tienda. Un Producto no publicado solo existe en el panel de administración.
_Evitar_: Activo, visible, habilitado

**Preview** (`Preview`):
Una imagen que muestra cómo se ve una Plantilla, usada para que el cliente la evalúe antes de comprar.
_Evitar_: Captura, screenshot, miniatura

**Preview principal** (`MainPreview`):
La primera Preview de una Plantilla, la que la representa en el catálogo. Un Paquete no tiene Previews propias: se muestra con las Previews principales de sus Plantillas.
_Evitar_: Portada, thumbnail

## Compra

**Compra como invitado** (`GuestCheckout`):
Forma de comprar en la que el cliente se identifica solo con su email, sin registrarse ni iniciar sesión.
_Evitar_: Checkout anónimo

**Compra** (`Order`):
Registro de que un email pagó un Producto. Solo existe cuando el pago está confirmado; un checkout abandonado no es una Compra. Da acceso a las Plantillas que el Producto incluía en el momento de pagar, aunque el Paquete cambie después.
_Evitar_: Pedido, orden, transacción, venta

**Enlace de compra** (`PurchaseLink`):
URL privada, enviada por email, que da acceso a las descargas de una Compra.
_Evitar_: Link de descarga, token, acceso

**Reembolsada** (`Refunded`):
Estado de una Compra cuyo dinero fue devuelto. Su Enlace de compra deja de dar acceso.
_Evitar_: Cancelada, anulada
