# 🎯 Comparación de Templates para 15 Tinas

## 📊 Tenemos 3 Diseños Disponibles

### 🔵 Opción 1: **AGRUPADO POR TINA** ⭐ **NUEVO - RECOMENDADO**
**Endpoint:** `POST /api/TemplatePresets/create-15-tinas-grouped`

```
┌─────────────────────────────────────────────────────────────┐
│ 🔵 TINA T1                              [Colapsar/Expandir]  │
├──────────┬─────────┬─────────┬─────────┬─────────┬──────────┤
│   HORA   │ PESO #1 │ PESO #2 │ PESO #3 │ PESO #4 │  TOTAL   │
├──────────┼─────────┼─────────┼─────────┼─────────┼──────────┤
│  08:00   │  120.5  │  150.3  │  180.0  │   95.2  │  546.0   │
│  08:15   │  165.0  │  170.5  │  155.0  │  160.0  │  650.5   │
│  08:30   │  200.0  │  210.0  │    0.0  │    0.0  │  410.0   │
├──────────┴─────────┴─────────┴─────────┴─────────┼──────────┤
│                          SUBTOTAL T1:             │ 1,606.5  │
└───────────────────────────────────────────────────┴──────────┘

┌─────────────────────────────────────────────────────────────┐
│ 🔵 TINA T2                              [Colapsar/Expandir]  │
├──────────┬─────────┬─────────┬─────────┬─────────┬──────────┤
│   HORA   │ PESO #1 │ PESO #2 │ PESO #3 │ PESO #4 │  TOTAL   │
├──────────┼─────────┼─────────┼─────────┼─────────┼──────────┤
│  09:00   │  200.0  │  210.5  │  190.0  │  185.5  │  786.0   │
│  09:30   │  220.0  │  230.0  │  215.0  │  225.0  │  890.0   │
├──────────┴─────────┴─────────┴─────────┴─────────┼──────────┤
│                          SUBTOTAL T2:             │ 1,676.0  │
└───────────────────────────────────────────────────┴──────────┘

... (T3 a T15 igual)

┌─────────────────────────────────────────────────────────────┐
│ 📊 RESUMEN GENERAL - TODAS LAS TINAS                         │
├──────────┬───────────────┬──────────────────────────────────┤
│   TINA   │  # REGISTROS  │     TOTAL ACUMULADO (kg)         │
├──────────┼───────────────┼──────────────────────────────────┤
│   T1     │       3       │            1,606.5               │
│   T2     │       2       │            1,676.0               │
│   T3     │       0       │                0.0               │
│   ...    │      ...      │               ...                │
│   T15    │       0       │                0.0               │
├──────────┴───────────────┼──────────────────────────────────┤
│      TOTAL GENERAL:      │            3,282.5        🏆     │
└──────────────────────────┴──────────────────────────────────┘
```

**✅ Ventajas:**
- ✅ **Cada tina tiene su espacio dedicado**
- ✅ Fácil navegación (pestañas o secciones colapsables)
- ✅ Subtotal visible por tina
- ✅ Puedes agregar múltiples filas con diferentes horas
- ✅ Ideal para llenar **tina por tina**
- ✅ No te pierdes entre filas
- ✅ Resumen general consolidado

**❌ Desventajas:**
- Más scroll vertical (15 secciones)
- No ves todas las tinas al mismo tiempo

**👍 Ideal para:** Operaciones donde trabajas **una tina a la vez** durante el día

---

### 🔸 Opción 2: **HORIZONTAL (TODO EN UNA TABLA)**
**Endpoint:** `POST /api/TemplatePresets/create-15-tinas`

```
┌────────────────────────────────────────────────────────────────────────────────┐
│          Registro de Producción por Tina (Múltiples Pesadas)                   │
├──────┬──────┬──────────┬──────────┬──────────┬──────────┬──────────┬──────────┤
│ HORA │ TINA │ PESO 1   │ PESO 2   │ PESO 3   │ PESO 4   │ PESO 5   │  TOTAL   │
├──────┼──────┼──────────┼──────────┼──────────┼──────────┼──────────┼──────────┤
│08:00 │  T1  │  120.5   │  150.3   │  180.0   │   95.2   │   0.0    │  546.0   │
│08:15 │  T1  │  165.0   │  170.5   │  155.0   │  160.0   │   0.0    │  650.5   │
│08:30 │  T1  │  200.0   │  210.0   │   0.0    │   0.0    │   0.0    │  410.0   │
│09:00 │  T2  │  200.0   │  210.5   │  190.0   │  185.5   │   0.0    │  786.0   │
│09:30 │  T2  │  220.0   │  230.0   │  215.0   │  225.0   │   0.0    │  890.0   │
│10:00 │  T5  │  300.0   │   0.0    │   0.0    │   0.0    │   0.0    │  300.0   │
│...   │ ...  │   ...    │   ...    │   ...    │   ...    │   ...    │   ...    │
└──────┴──────┴──────────┴──────────┴──────────┴──────────┴──────────┴──────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│                    Resumen por Tina (Calculado Automáticamente)              │
├──────┬──────────┬────────┬────────┬────────┬────────┬────────┬──────────────┤
│ TINA │ ENTRADAS │ PESO 1 │ PESO 2 │ PESO 3 │ PESO 4 │ PESO 5 │ TOTAL ACUM.  │
├──────┼──────────┼────────┼────────┼────────┼────────┼────────┼──────────────┤
│  T1  │    3     │ 485.5  │ 530.8  │ 335.0  │ 255.2  │  0.0   │   1,606.5    │
│  T2  │    2     │ 420.0  │ 440.5  │ 405.0  │ 410.5  │  0.0   │   1,676.0    │
│  T3  │    0     │  0.0   │  0.0   │  0.0   │  0.0   │  0.0   │       0.0    │
│ ...  │   ...    │  ...   │  ...   │  ...   │  ...   │  ...   │      ...     │
│  T15 │    0     │  0.0   │  0.0   │  0.0   │  0.0   │  0.0   │       0.0    │
└──────┴──────────┴────────┴────────┴────────┴────────┴────────┴──────────────┘
```

**✅ Ventajas:**
- ✅ Todas las entradas en una tabla
- ✅ Vista general de todo
- ✅ Fácil comparar entre tinas
- ✅ Menos navegación

**❌ Desventajas:**
- Puede ser confuso con muchas filas
- No hay separación visual por tina
- Más ancho (muchas columnas)

**👍 Ideal para:** Operaciones donde quieres ver **todas las tinas en un solo lugar**

---

### 🔹 Opción 3: **SIMPLE (3 COLUMNAS)**
**Endpoint:** `POST /api/TemplatePresets/create-simple-3-columns`

```
┌─────────────────────────────────────────────────────┐
│      Registro Simple - 3 Columnas                    │
├──────────┬──────────┬──────────────────────────────┤
│   HORA   │   TINA   │      PESO NETO (kg)          │
├──────────┼──────────┼──────────────────────────────┤
│  08:00   │    T1    │           120.5              │
│  08:15   │    T1    │           165.0              │
│  08:30   │    T1    │           200.0              │
│  09:00   │    T2    │           200.0              │
│  09:30   │    T2    │           220.0              │
│  10:00   │    T5    │           300.0              │
│   ...    │   ...    │            ...               │
└──────────┴──────────┴──────────────────────────────┘

┌─────────────────────────────────────────────────────┐
│              Resumen por Tina                        │
├──────────┬──────────────────────────────────────────┤
│   TINA   │        TOTAL PESO NETO (kg)              │
├──────────┼──────────────────────────────────────────┤
│    T1    │               485.5                      │
│    T2    │               420.0                      │
│    T3    │                 0.0                      │
│   ...    │                ...                       │
│   T15    │                 0.0                      │
└──────────┴──────────────────────────────────────────┘
```

**✅ Ventajas:**
- ✅ Muy simple
- ✅ Solo 3 columnas
- ✅ Registro rápido
- ✅ Menos espacio

**❌ Desventajas:**
- Solo una columna de peso
- No permite múltiples pesadas por entrada

**👍 Ideal para:** Operaciones **muy simples** con solo un peso por entrada

---

## 🎯 ¿Cuál Elegir?

| Escenario | Template Recomendado |
|-----------|---------------------|
| **Trabajas tina por tina durante el día** | ⭐ **AGRUPADO** (Opción 1) |
| **Quieres ver todo de un vistazo** | HORIZONTAL (Opción 2) |
| **Solo registras un peso por entrada** | SIMPLE (Opción 3) |
| **Cada tina tiene múltiples pesadas** | ⭐ **AGRUPADO** (Opción 1) |
| **Necesitas subtotales por tina visibles** | ⭐ **AGRUPADO** (Opción 1) |

---

## 🚀 Cómo Crear Cada Template

### 1️⃣ Template AGRUPADO (Recomendado según tu imagen):
```bash
POST http://localhost:5074/api/TemplatePresets/create-15-tinas-grouped
Content-Type: application/json

{}
```

### 2️⃣ Template HORIZONTAL:
```bash
POST http://localhost:5074/api/TemplatePresets/create-15-tinas
Content-Type: application/json

{}
```

### 3️⃣ Template SIMPLE:
```bash
POST http://localhost:5074/api/TemplatePresets/create-simple-3-columns
Content-Type: application/json

{}
```

### 📋 Ver todos los presets disponibles:
```bash
GET http://localhost:5074/api/TemplatePresets/available
```

---

## 📊 Comparación Técnica

| Característica | AGRUPADO | HORIZONTAL | SIMPLE |
|---------------|----------|------------|--------|
| **# Secciones** | 15 + resumen | 1 + resumen | 1 + resumen |
| **Columnas de peso** | 4 por tina | 5 globales | 1 |
| **Subtotal por tina** | ✅ Visible | ❌ Solo en resumen | ❌ Solo en resumen |
| **Navegación** | Por pestañas/secciones | Scroll vertical | Scroll vertical |
| **Colapsable** | ✅ Sí | ❌ No | ❌ No |
| **Complejidad** | Media | Alta | Baja |
| **Recomendado para imagen** | ⭐ **SÍ** | No | No |

---

## 📝 Según Tu Imagen

Basado en tu imagen donde muestras:
```
Hora n:m | Hora n:n
Tina #n  | Tina #n1
Peso #   | Peso #
#        | #
-        | -
```

El template **AGRUPADO (Opción 1)** es el que **más se acerca** a tu diseño porque:
- ✅ Cada tina tiene su propia sección
- ✅ Puedes agregar múltiples filas de hora/peso por tina
- ✅ Cada tina muestra su subtotal
- ✅ Resumen general al final

---

¿Quieres que compile el backend y probemos el endpoint **create-15-tinas-grouped**? 🚀
