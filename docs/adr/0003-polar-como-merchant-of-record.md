# Polar.sh como Merchant of Record

Los pagos se procesan con Polar.sh, que actúa como Merchant of Record: le vende legalmente al comprador, se encarga de los impuestos internacionales y nos paga a una cuenta bancaria en Ecuador. Usamos su checkout alojado y su webhook `order.paid`, y la entrega de archivos es nuestra. Se descartaron estas alternativas: Stripe, porque no acepta vendedores de Ecuador sin una LLC en EE. UU.; Paddle, porque su política excluye plantillas y productos que no son software; los procesadores ecuatorianos (Payphone, Kushki), porque casi no sirven para compradores fuera de Ecuador; y PayPal directo, porque los impuestos quedarían a nuestro cargo. La integración vive detrás de `IPaymentProvider`, con Lemon Squeezy como respaldo si Polar deja de servir.

## Consecuencias

- Solo se acepta tarjeta: ningún Merchant of Record ofrece medios de pago locales de LatAm.
- Facturamos a Polar como exportación de servicios, no a cada comprador.
