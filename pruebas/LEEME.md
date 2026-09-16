# Pruebas de la API

`Frigolab.postman_collection.json` — 13 pedidos, 42 verificaciones.
Comprueba los tres arreglos de inventario, trazabilidad e indicadores.

## Con Postman (lo más simple)

1. **Import** → arrastrar `Frigolab.postman_collection.json`.
2. Abrir la colección → pestaña **Variables** → revisar `baseUrl`.
   - Planta: `http://100.74.186.82:8096/api` (viene puesto)
   - Local: `http://localhost:5099/api`
3. Botón **Run collection** → **Run**.

Al final se ve el resumen: cuántas verificaciones pasaron y cuáles no.
Los mensajes con detalle (tamaños, tiempos, qué formularios tienen la fecha
corrida) salen en la **Console** de Postman (`Ctrl+Alt+C`).

## Por consola, sin instalar nada

```bash
npx newman run backend-frigo/pruebas/Frigolab.postman_collection.json \
  --env-var baseUrl=http://100.74.186.82:8096/api
```

## Qué prueba cada carpeta

**1 · Lotes** — Manda dos formularios distintos con el mismo número de lote y
verifica que el segundo **sume** (1000 + 500 = 1500 lb) en vez de descartarse.
Después re-manda el segundo, como si alguien editara y volviera a guardar, y
verifica que **no cuente dos veces**. Revisa también que el kardex quede con las
dos entradas y el saldo corrido (700 → 1100).

Usa un lote de prueba con nombre único por corrida (`TEST-<timestamp>`) y lo
borra al final junto con sus movimientos. **No toca datos reales.**

**2 · Trazabilidad** — Verifica que cada paso traiga `fecha_registro`, que es la
fecha que escribió el operario, y que `created_at` siga estando como sello de
auditoría. En la consola lista los formularios donde las dos fechas no coinciden.

Si la base que se está probando no tiene datos de los últimos 60 días (una copia
de desarrollo, por ejemplo), pasarle un rango:

```bash
npx newman run ... --env-var desde=2026-04-01 --env-var hasta=2026-06-01
```

**3 · Indicadores** — Verifica que la carga traiga los borradores, el reloj del
servidor y la lista de borradores abiertos; y que el refresco incremental
devuelva solo lo que cambió (compara el tamaño de las dos respuestas).

## Prueba de dos usuarios a la vez

Postman manda un pedido detrás de otro, así que no puede probar lo que pasa
cuando **dos operarios guardan al mismo tiempo**. Para eso está aparte:

```bash
node backend-frigo/pruebas/dos-usuarios.mjs
```

Lanza los pedidos en paralelo de verdad y comprueba tres cosas:

1. Dos operarios guardan el PD-04 del mismo lote del día → tiene que quedar
   **un solo lote sumado**, no dos filas con el mismo número.
2. Un lote de 100 lb y dos operarios que descuentan 80 lb cada uno → **solo uno
   puede pasar**; al otro se le niega por saldo insuficiente.
3. El usuario 1 cierra su PD-04 y el usuario 2 abre el formulario siguiente →
   el lote **ya le aparece** en su lista de disponibles.

Contra otra API: `API=http://100.74.186.82:8096/api node ...`. Usa lotes
`TEST-2U-*` con nombre único por corrida y los borra al terminar.

## Ojo antes de correrla contra planta

La carpeta 1 y la prueba de dos usuarios **van a fallar en planta hasta que se despliegue el backend**: hoy el
servidor todavía descarta el segundo formulario en vez de sumarlo, que es
justamente lo que la prueba detecta. Las carpetas 2 y 3 fallan igual hasta el
deploy.

Para probar antes de desplegar, levantar la API local:

```bash
cd backend-frigo
dotnet run --project FormBuilder.API.csproj --urls http://localhost:5099
```

y correr la colección con `baseUrl=http://localhost:5099/api`.

## Contrato de la API (el "swagger")

La API publica su documento OpenAPI en:

```
http://100.74.186.82:8096/openapi/v1.json
```

Se puede importar en Postman (**Import → Link**) para tener todos los endpoints
sin escribirlos a mano. No agrega dependencias: usa el paquete
`Microsoft.AspNetCore.OpenApi` que el proyecto ya tenía.
