# Hallazgos observados durante las pruebas

Este archivo registra comportamiento real antes de proponer cambios de lógica de negocio. No se modificó la lógica de Sora para hacer pasar la suite.

## QA-PRD-01 · Mensaje de error para código duplicado

- **Evidencia:** prueba unitaria `UT-PRD-02`. `ProductService.CreateAsync` detecta que el código interno ya existe y no persiste el producto; devuelve `product_code_generation_failed`. `ProductsController` responde HTTP 409 Conflict.
- **Impacto observado:** la regla de no duplicar se cumple. El mensaje/código reportado dice que falló la generación aunque el usuario haya escrito un código repetido; la interfaz puede mostrar una explicación confusa.
- **Propuesta, pendiente de autorización:** distinguir en `ResolveCodeAsync` un código ingresado que ya existe y devolver `product_code_already_exists`; reservar `product_code_generation_failed` para cuando el generador aleatorio agote sus intentos. Después habría que actualizar la prueba y verificar el mensaje de interfaz.
- **Estado:** documentado; no corregido.

Las fallas iniciales de las pruebas de descuento y stock se debieron a expectativas de prueba equivocadas: el tope del descuento ya era Bs 0,40 para un margen de Bs 2,00, y el ajuste se intentaba sobre un saldo con diez unidades reservadas. Se corrigieron las expectativas y los datos del test; no hubo cambio de lógica de negocio.
