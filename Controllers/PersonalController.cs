using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FormBuilder.API.Data;
using FormBuilder.API.Models;
using FormBuilder.API.Services;
using System.Linq;   // .Where y .Any en la deduplicación de procesos
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FormBuilder.API.Controllers
{
    /// <summary>
    /// API del módulo de Personal: registra personal de planta/externo por proceso
    /// y rango de horario, valida el estándar de tiempo del proceso (sin IA, solo
    /// comparación de rangos) y expone indicadores agregados en SQL para no
    /// sobrecargar el sistema (mismo criterio que Consumptions/LotesInventario).
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class PersonalController : ControllerBase
    {
        /// <summary>
        /// Fin del día para un filtro de fechas.
        ///
        /// El navegador manda solo la fecha ("2026-09-07"), que .NET interpreta
        /// como las 00:00:00. Comparar con &lt;= dejaba fuera todo lo registrado
        /// ese día después de medianoche: buscar "del 7 al 7" no devolvía nada y
        /// había que estirar el rango hasta el 8.
        ///
        /// Se usa AddDays(1).AddTicks(-1) y no 23:59:59 para no perder los
        /// milisegundos de un registro de las 23:59:59.500.
        /// </summary>
                /// <summary>
        /// Fecha real del formulario: la que escribió el operario en el
        /// encabezado. Solo si no hay ninguna se cae a CreatedAt.
        ///
        /// Son cosas distintas: el formulario del día 7 puede guardarse el 8, y
        /// filtrar por la fecha de guardado deja fuera lo que el usuario busca.
        /// Se descartan los campos de vencimiento y versión, que también dicen
        /// "fecha" pero no son la del registro.
        /// </summary>
        private static DateTime FechaDelFormulario(FilledForm form)
        {
            var respaldo = form.CreatedAt.Date;
            if (string.IsNullOrWhiteSpace(form.HeaderData)) return respaldo;

            try
            {
                var header = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(form.HeaderData);
                if (header == null) return respaldo;

                foreach (var (clave, valor) in header)
                {
                    var k = clave.ToUpperInvariant();
                    if (!k.Contains("FECHA")) continue;
                    if (k.Contains("VENCIM") || k.Contains("CADUC") || k.Contains("VERSION")
                        || k.Contains("EXPIR") || k.Contains("NACIM")) continue;

                    var texto = valor.ValueKind == JsonValueKind.String ? valor.GetString() : valor.ToString();
                    if (string.IsNullOrWhiteSpace(texto)) continue;

                    if (DateTime.TryParse(texto, System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None, out var fecha))
                        return fecha.Date;

                    // Formato dd/MM/yyyy, que es como lo escriben en planta.
                    if (DateTime.TryParseExact(texto.Trim(), new[] { "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy" },
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None, out var fechaDmy))
                        return fechaDmy.Date;
                }
            }
            catch
            {
                // HeaderData corrupto: se usa la de guardado en vez de romper el listado.
            }

            return respaldo;
        }

        /// <summary>
        /// Horas entre el inicio y el fin de la jornada, contemplando los turnos
        /// que cruzan la medianoche.
        ///
        /// Antes se exigía que el fin fuera mayor que el inicio, así que un turno
        /// de 17:00 a 00:30 devolvía null y en pantalla salía "—": justamente los
        /// turnos de noche, que son los que más interesa controlar.
        ///
        /// Si el fin es menor que el inicio se asume que pasó al día siguiente y
        /// se le suman 24 horas: 17:00 → 00:30 da 7.5 h.
        ///
        /// Un turno de más de 18 horas casi siempre es un error de digitación
        /// (16:00 en vez de 06:00), así que se descarta en vez de inflar el
        /// indicador de horas-hombre.
        /// </summary>
        /// <summary>
        /// Campos del encabezado donde puede estar el proceso productivo, en
        /// orden de preferencia.
        ///
        /// "ACTIVIDAD DE PROCESO" es el combo que elige el operario al llenar;
        /// "TIPO DE PROCESO" lo usan los formularios de productividad
        /// (PD-14, PD-20, PD-21).
        ///
        /// "TIPO" a secas queda fuera a propósito: en el PD-06 vale
        /// "Provisional", que es una condición del producto y no una actividad.
        /// Incluirlo llenaba la columna de valores que no son procesos.
        /// </summary>
        private static readonly string[] CamposProceso =
            { "ACTIVIDAD DE PROCESO", "TIPO DE PROCESO", "PROCESO PRODUCTIVO", "PROCESO", "ACTIVIDAD" };

        /// <summary>
        /// Actividades que la plantilla declara en «Proceso - Productivo».
        ///
        /// Se guardan en el campo Supervisa, separadas por coma o salto de
        /// línea. Es lo que cubre ese formulario de forma permanente, mientras
        /// que el encabezado solo trae la actividad que el operario eligió esa
        /// vez —y muchas veces no elige ninguna.
        ///
        /// Con varias actividades se muestran juntas: un formulario que cubre
        /// Fileteo y Empaque es de los dos, y quedarse con la primera sería
        /// esconder la mitad.
        /// </summary>
        private static readonly HashSet<string> PlaceholdersSupervisa =
            new(StringComparer.OrdinalIgnoreCase)
            { "Proceso - Productivo", "Proceso Productivo", "Proceso-Productivo" };

        private static string ActividadesDePlantilla(Template? template)
        {
            var crudo = template?.Supervisa;
            if (string.IsNullOrWhiteSpace(crudo)) return "";

            var partes = crudo
                .Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => System.Text.RegularExpressions.Regex.Replace(x.Trim(), @"\s+", " "))
                .Where(x => x.Length > 0)
                // Se descarta la etiqueta del campo cuando quedó guardada como
                // si fuera un valor: hay plantillas con "Proceso - Productivo"
                // literal adentro, y mostrar eso en la columna no dice nada.
                // Mismo criterio que PLACEHOLDERS_SUPERVISA del frontend.
                .Where(x => !PlaceholdersSupervisa.Contains(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return partes.Count == 0 ? "" : string.Join(" · ", partes);
        }

        /// <summary>
        /// Proceso productivo del formulario: lo que escribió el operario en el
        /// encabezado. Si no hay nada, se cae al de la plantilla.
        ///
        /// Se descarta LOTE DE PROCESO, que contiene un número de lote y no el
        /// nombre de una actividad.
        /// </summary>
        /// <summary>
        /// Columnas de peso producido.
        ///
        /// Cada formato lo llama distinto: el PD-14 usa "PESO PROCESADO", el
        /// PD-15 "PESO PROCESO", el PD-06 "TOTAL Lbs NETAS" y el PD-04 "Peso
        /// Neto Total". Con una lista corta la columna salía vacía en la mitad
        /// de los formatos.
        /// </summary>
        private static readonly Regex RxPesoProd = new(
            @"total\s*lbs?\s*netas?|lbs?\s*netas?|peso\s*neto\s*total|peso\s*neto|"
          + @"total\s*producido|peso\s*producci[oó]n|peso\s*proces\w*|"
          // PRODUCTO TERMINADO, en todas sus formas.
          //
          // Cada formato lo escribe distinto: "LIBRAS PRODUCTO TERMINADO" en el
          // PD-14, "PESO PT" en el PD-19C, "P. TERMINADO" en otros. Buscar solo
          // la forma larga dejaba la columna vacía en la mitad de los formatos.
          //
          // El \b final del abreviado importa: sin él, "PESO PTO SALIDA" o
          // cualquier palabra que empiece con PT entraría por error.
          + @"producto\s*terminado|prod\.?\s*terminado|p\.?\s*terminado|"
          + @"\bp\.?\s*t\.?\b|"
          + @"subtotal\s*\(?\s*lbs?|total\s*lbs?|peso\s*total",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Columnas que NO son peso producido aunque su nombre se le parezca.
        ///
        /// "CAPACIDAD CAJAS-TINAS / Lbs" es cuánto entra en una caja, no cuánto
        /// se produjo: sumarla infla el total con un dato de capacidad. Lo mismo
        /// con los pesos de materia prima y los descuentos.
        /// </summary>
        private static readonly Regex RxNoEsPeso = new(
            @"capacidad|materia\s*prima|bruto|tara|merma|desperdicio|da[ñn]ado|devuelto|"
          // MATERIA PRIMA, también en todas sus formas. Es la ENTRADA del
          // proceso: sumarla como producción contaría dos veces el mismo
          // pescado, una al recibirlo y otra al procesarlo.
          //
          // Va en las EXCLUSIONES y se evalúa después, así que gana sobre
          // cualquier coincidencia de arriba.
          + @"materia\s*prima|mat\.?\s*prima|\bm\.?\s*p\.?\b|"
          + @"por\s*caja|unitario|est[áa]ndar|subproducto",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Tablas donde puede estar la producción.
        ///
        /// Incluye "personal" porque en el PD-14 y el PD-15 el peso se anota en
        /// la misma tabla de control de personal, no en una de producción
        /// aparte: sin esto esos dos formatos salían con el peso en blanco.
        ///
        /// También se aceptan las tablas SIN título: varias plantillas las
        /// dejaron así y descartarlas perdía el dato.
        /// </summary>
        /// <summary>
        /// Tablas de PRODUCCIÓN propiamente dicha, sin incluir la de personal.
        ///
        /// El PD-14 tiene el peso en las dos: "LIBRAS PRODUCTO TERMINADO" en
        /// Control Producción y "PESO PROCESADO" en CONTROL PERSONAL. Sumarlas
        /// daba el total DUPLICADO.
        ///
        /// Manda la de producción; la de personal solo se usa si el formulario
        /// no tiene tabla de producción (caso del PD-15).
        /// </summary>
        private static readonly Regex RxTablaProduccion = new(
            @"producci[oó]n|proceso|empaque|reempaque|liberaci[oó]n|salida|despacho",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>La tabla de control de personal, como respaldo de peso.</summary>
        private static readonly Regex RxTablaPersonal = new(
            @"personal|mano\s*de\s*obra",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Libras producidas: se suman de la tabla de producción.
        ///
        /// Devuelve null y no 0 cuando el formulario no declara peso. Un cero se
        /// leería como "produjo nada", que es una afirmación distinta de "no lo
        /// registra".
        /// </summary>
        /// <summary>Columnas que identifican el producto en la tabla de producción.</summary>
        private static readonly Regex RxColumnaProducto = new(
            @"producto|talla|especie|presentaci[oó]n|clasificaci[oó]n",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Peso producido DESGLOSADO POR PRODUCTO.
        ///
        /// El total solo dice "152 libras". El desglose dice "Robalo 41,
        /// Silver sea bass 111", que es lo que se necesita para costear: no es
        /// lo mismo producir 41 libras de un filete caro que de uno barato.
        ///
        /// Devuelve una lista vacía si el formulario no declara productos, y
        /// entonces se usa el total suelto.
        /// </summary>
        private static List<(string Producto, decimal Peso)> PesoPorProducto(FilledForm form)
        {
            var salida = new List<(string, decimal)>();
            if (string.IsNullOrWhiteSpace(form.BodyData)) return salida;

            try
            {
                using var doc = JsonDocument.Parse(form.BodyData);
                if (doc.RootElement.ValueKind != JsonValueKind.Array) return salida;

                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    if (el.ValueKind != JsonValueKind.Object) continue;

                    var titulo = el.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                    if (!string.IsNullOrWhiteSpace(titulo) && RxMateriaPrima.IsMatch(titulo)) continue;
                    // Solo la tabla de producción: la de personal registra el
                    // peso por franja y no tiene el producto al lado.
                    if (!string.IsNullOrWhiteSpace(titulo) && RxTablaPersonal.IsMatch(titulo)) continue;
                    if (!string.IsNullOrWhiteSpace(titulo) && !RxTablaProduccion.IsMatch(titulo)) continue;

                    if (!FilasDeTabla(el, out var filas)) continue;

                    foreach (var fila in filas.EnumerateArray())
                    {
                        if (fila.ValueKind != JsonValueKind.Object) continue;

                        string producto = "";
                        decimal peso = 0;

                        foreach (var prop in fila.EnumerateObject())
                        {
                            var n = prop.Name;

                            if (RxPesoProd.IsMatch(n) && !RxNoEsPeso.IsMatch(n))
                            {
                                var v = ParseDecimal(prop.Value);
                                if (v > 0) peso += v;
                            }
                            else if (producto.Length == 0 && RxColumnaProducto.IsMatch(n) && !RxNoEsPeso.IsMatch(n))
                            {
                                var texto = prop.Value.ValueKind == JsonValueKind.String
                                    ? prop.Value.GetString() : prop.Value.ToString();
                                if (!string.IsNullOrWhiteSpace(texto)) producto = texto!.Trim();
                            }
                        }

                        if (peso > 0)
                            salida.Add((string.IsNullOrWhiteSpace(producto) ? "(sin producto)" : producto, peso));
                    }
                }
            }
            catch { /* BodyData corrupto */ }

            return salida;
        }

        /// <summary>
        /// Libras producidas del formulario, sumadas de sus tablas de producción.
        ///
        /// ⚠️ BodyData NO guarda el título de las tablas, solo su "id". Antes se
        /// leía "title" directo de BodyData, siempre venía vacío, y los filtros
        /// de materia prima / producción nunca se aplicaban: el PD-04 sumaba
        /// "PESO NETO" + "Peso Total" del Resumen (total duplicado). Ahora el
        /// título se busca en la plantilla por id (TitulosDeTablas).
        ///
        /// Dos pasadas para no dejar vacío ningún formato que hoy sí sale:
        ///   1ª: solo tablas cuyo título es de producción (o sin título).
        ///   2ª: si la 1ª no encontró nada, cualquier tabla (es lo que hacía
        ///       antes en la práctica, porque el título nunca se leía). Así
        ///       ningún formato que hoy muestra peso se queda vacío.
        ///
        /// Las columnas marcadas en la plantilla mandan: "pesoProducido" se
        /// cuenta siempre, cualquier otra marca no se cuenta nunca.
        ///
        /// Devuelve null y no 0 cuando el formulario no declara peso.
        /// </summary>
        private static decimal? PesoDeProduccion(FilledForm form)
        {
            if (string.IsNullOrWhiteSpace(form.BodyData)) return null;

            try
            {
                using var doc = JsonDocument.Parse(form.BodyData);
                if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;

                var titulos = TitulosDeTablas(form);
                var roles = RolesDeColumnas(form.Template);

                var estructura = ColumnasDeTablas(form);

                return SumarPesoProducido(doc.RootElement, titulos, roles, estructura, soloProduccion: true)
                    ?? SumarPesoProducido(doc.RootElement, titulos, roles, estructura, soloProduccion: false);
            }
            catch
            {
                return null;   // BodyData corrupto: mejor vacío que un dato inventado
            }
        }

        private static decimal? SumarPesoProducido(
            JsonElement raiz,
            Dictionary<string, string> titulos,
            Dictionary<string, string> roles,
            (Dictionary<string, List<ColumnaPlantilla>> PorId, List<List<ColumnaPlantilla>> PorPosicion) estructura,
            bool soloProduccion)
        {
            decimal total = 0;
            var encontro = false;
            var posicion = -1;

            foreach (var el in raiz.EnumerateArray())
            {
                posicion++;
                if (el.ValueKind != JsonValueKind.Object) continue;
                if (!FilasDeTabla(el, out var filas)) continue;

                // Columna calculada de peso (ej. TOTAL Lbs NETAS): si quedó en 0 se
                // calcula, y sus entradas de peso (ej. PESO PT) no se suman aparte.
                var plan = PlanDePeso(el, posicion, estructura);

                var titulo = TituloDeTabla(el, titulos);
                var tieneTitulo = !string.IsNullOrWhiteSpace(titulo);

                // En la 1ª pasada solo cuentan las tablas de producción: la de
                // MATERIA PRIMA tiene pesos de entrada y un "Resumen" repite el
                // total. Sin título (plantilla vieja o id que ya no existe) se acepta.
                var tablaValida = !soloProduccion
                    || !tieneTitulo
                    || (RxTablaProduccion.IsMatch(titulo!) && !RxMateriaPrima.IsMatch(titulo!));

                bool Cuenta(string nombre)
                {
                    var rol = roles.TryGetValue(nombre.Trim(), out var r) ? r : null;
                    if (rol == "pesoProducido") return true;     // la marca manda
                    if (rol != null) return false;               // marcada como otra cosa
                    return tablaValida && RxPesoProd.IsMatch(nombre) && !RxNoEsPeso.IsMatch(nombre);
                }

                foreach (var fila in filas.EnumerateArray())
                {
                    if (fila.ValueKind != JsonValueKind.Object) continue;
                    var textos = plan.TieneFormulas ? TextosDeFila(fila) : null;

                    foreach (var prop in fila.EnumerateObject())
                    {
                        if (plan.EsEntrada(prop.Name)) continue;   // ya está dentro de la calculada
                        if (!Cuenta(prop.Name)) continue;

                        var v = ParseDecimal(prop.Value);
                        if (textos != null && plan.FormulaDe(prop.Name) != null)
                            v = plan.PesoCalculado(prop.Name, v, textos);
                        if (v > 0) { total += v; encontro = true; }
                    }

                    // Columna calculada que la fila ni siquiera trae.
                    if (textos != null)
                    {
                        foreach (var ausente in plan.CalculadasAusentes(textos.Keys))
                        {
                            if (!Cuenta(ausente)) continue;
                            var v = plan.PesoCalculado(ausente, 0, textos);
                            if (v > 0) { total += v; encontro = true; }
                        }
                    }
                }
            }

            return encontro ? total : (decimal?)null;
        }

        /// <summary>
        /// Filas de una tabla de BodyData. Si trae "rows" (lo que se ve y se edita
        /// en Ver / Editar Formulario Llenado) manda "rows"; si no, "data".
        /// Registros guardados con las dos listas distintas mostraban en el
        /// reporte filas que ya se habían quitado y no las que se agregaron.
        /// </summary>
        private static bool FilasDeTabla(JsonElement el, out JsonElement filas)
        {
            if (el.ValueKind == JsonValueKind.Object)
            {
                if (el.TryGetProperty("rows", out filas) && filas.ValueKind == JsonValueKind.Array) return true;
                if (el.TryGetProperty("data", out filas) && filas.ValueKind == JsonValueKind.Array) return true;
            }
            filas = default;
            return false;
        }

        /// <summary>Id de un elemento como texto: la plantilla usa números y textos.</summary>
        private static string? IdDeElemento(JsonElement el)
        {
            if (!el.TryGetProperty("id", out var id)) return null;
            return id.ValueKind switch
            {
                JsonValueKind.String => id.GetString(),
                JsonValueKind.Number => id.GetRawText(),
                _ => null
            };
        }

        /// <summary>
        /// Título de cada tabla del formulario, por id.
        ///
        /// Se lee de la plantilla actual y, para los ids que ya no estén ahí
        /// (tablas borradas o renombradas después), del TemplateSnapshot que se
        /// guardó cuando se llenó el formulario.
        /// </summary>
        /// <summary>
        /// Columnas de cada tabla según la plantilla: por id y por posición.
        /// Primero la plantilla actual; los ids que ya no están se toman del
        /// snapshot guardado con el formulario.
        /// </summary>
        private static (Dictionary<string, List<ColumnaPlantilla>> PorId, List<List<ColumnaPlantilla>> PorPosicion)
            ColumnasDeTablas(FilledForm form)
        {
            var porId = new Dictionary<string, List<ColumnaPlantilla>>(StringComparer.OrdinalIgnoreCase);
            var porPosicion = new List<List<ColumnaPlantilla>>();

            static string? Texto(JsonElement o, string prop) =>
                o.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

            void CargarLista(JsonElement lista, bool conPosiciones)
            {
                if (lista.ValueKind != JsonValueKind.Array) return;
                foreach (var el in lista.EnumerateArray())
                {
                    var cols = new List<ColumnaPlantilla>();
                    if (el.ValueKind == JsonValueKind.Object
                        && el.TryGetProperty("columns", out var c) && c.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var col in c.EnumerateArray())
                        {
                            if (col.ValueKind != JsonValueKind.Object) continue;
                            cols.Add(new ColumnaPlantilla
                            {
                                Label = Texto(col, "label") ?? Texto(col, "header") ?? Texto(col, "name"),
                                Tipo = Texto(col, "type"),
                                Formula = Texto(col, "formula"),
                                Rol = Texto(col, "rol"),
                            });
                        }
                    }
                    if (conPosiciones) porPosicion.Add(cols);
                    var id = el.ValueKind == JsonValueKind.Object ? IdDeElemento(el) : null;
                    if (!string.IsNullOrWhiteSpace(id) && !porId.ContainsKey(id!)) porId[id!] = cols;
                }
            }

            void CargarJson(string? json, bool conPosiciones)
            {
                if (string.IsNullOrWhiteSpace(json)) return;
                try
                {
                    using var doc = JsonDocument.Parse(json);
                    var raiz = doc.RootElement;
                    if (raiz.ValueKind == JsonValueKind.String)
                    {
                        using var interno = JsonDocument.Parse(raiz.GetString() ?? "[]");
                        CargarLista(interno.RootElement, conPosiciones);
                    }
                    else CargarLista(raiz, conPosiciones);
                }
                catch { /* JSON corrupto: se sigue con lo que haya */ }
            }

            CargarJson(form.Template?.BodyElements, conPosiciones: true);

            if (!string.IsNullOrWhiteSpace(form.TemplateSnapshot))
            {
                try
                {
                    using var snap = JsonDocument.Parse(form.TemplateSnapshot);
                    if (snap.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var p in snap.RootElement.EnumerateObject())
                        {
                            if (!p.Name.Equals("BodyElements", StringComparison.OrdinalIgnoreCase)) continue;
                            if (p.Value.ValueKind == JsonValueKind.String) CargarJson(p.Value.GetString(), conPosiciones: false);
                            else CargarLista(p.Value, conPosiciones: false);
                            break;
                        }
                    }
                }
                catch { /* snapshot corrupto */ }
            }

            return (porId, porPosicion);
        }

        /// <summary>¿La columna cuenta como peso producido? La marca manda; sin marca, el nombre.</summary>
        private static bool EsColumnaDePeso(string etiqueta, string? rol)
        {
            if (rol == "pesoProducido") return true;
            if (!string.IsNullOrWhiteSpace(rol)) return false;
            return RxPesoProd.IsMatch(etiqueta) && !RxNoEsPeso.IsMatch(etiqueta);
        }

        /// <summary>
        /// Cómo sacar el peso de una tabla con columna calculada (ver
        /// CalculoPesoTabla). Sin columnas calculadas de peso devuelve "Ninguno".
        /// </summary>
        private static CalculoPesoTabla PlanDePeso(
            JsonElement tabla, int posicion,
            (Dictionary<string, List<ColumnaPlantilla>> PorId, List<List<ColumnaPlantilla>> PorPosicion) estructura)
        {
            var id = IdDeElemento(tabla);
            List<ColumnaPlantilla>? cols = null;
            if (id != null) estructura.PorId.TryGetValue(id, out cols);
            if (cols == null && id == null && posicion >= 0 && posicion < estructura.PorPosicion.Count)
                cols = estructura.PorPosicion[posicion];
            return cols == null || cols.Count == 0
                ? CalculoPesoTabla.Ninguno
                : CalculoPesoTabla.Crear(cols, EsColumnaDePeso);
        }

        /// <summary>Celdas de una fila como texto, para evaluar fórmulas.</summary>
        private static Dictionary<string, string> TextosDeFila(JsonElement fila)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (fila.ValueKind != JsonValueKind.Object) return d;
            foreach (var p in fila.EnumerateObject())
            {
                if (d.ContainsKey(p.Name)) continue;
                d[p.Name] = p.Value.ValueKind switch
                {
                    JsonValueKind.String => p.Value.GetString() ?? "",
                    JsonValueKind.Number => p.Value.GetRawText(),
                    _ => ""
                };
            }
            return d;
        }

        private static Dictionary<string, string> TitulosDeTablas(FilledForm form)
        {
            var titulos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            void CargarLista(JsonElement lista)
            {
                if (lista.ValueKind != JsonValueKind.Array) return;
                foreach (var el in lista.EnumerateArray())
                {
                    if (el.ValueKind != JsonValueKind.Object) continue;
                    var id = IdDeElemento(el);
                    if (string.IsNullOrWhiteSpace(id) || titulos.ContainsKey(id!)) continue;
                    var titulo = el.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String
                        ? t.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(titulo)) titulos[id!] = titulo!.Trim();
                }
            }

            void CargarJson(string? json)
            {
                if (string.IsNullOrWhiteSpace(json)) return;
                try
                {
                    using var doc = JsonDocument.Parse(json);
                    CargarLista(doc.RootElement);
                }
                catch { /* JSON corrupto: se sigue con lo que haya */ }
            }

            // 1º la plantilla actual
            CargarJson(form.Template?.BodyElements);

            // 2º el snapshot: {"BodyElements": "[...]"} o {"BodyElements": [...]}
            if (!string.IsNullOrWhiteSpace(form.TemplateSnapshot))
            {
                try
                {
                    using var snap = JsonDocument.Parse(form.TemplateSnapshot);
                    if (snap.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var p in snap.RootElement.EnumerateObject())
                        {
                            if (!p.Name.Equals("BodyElements", StringComparison.OrdinalIgnoreCase)) continue;
                            if (p.Value.ValueKind == JsonValueKind.String) CargarJson(p.Value.GetString());
                            else CargarLista(p.Value);
                            break;
                        }
                    }
                }
                catch { /* snapshot corrupto */ }
            }

            return titulos;
        }

        /// <summary>Título de una tabla de BodyData: el propio si lo trae, si no el de la plantilla.</summary>
        private static string? TituloDeTabla(JsonElement el, Dictionary<string, string> titulos)
        {
            if (el.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(t.GetString()))
                return t.GetString()!.Trim();

            var id = IdDeElemento(el);
            return id != null && titulos.TryGetValue(id, out var titulo) ? titulo : null;
        }

        private static readonly Regex RxMateriaPrima = new(
            @"materia\s*prima|recepci[oó]n|ingreso",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>Campos del encabezado donde puede estar el destino.</summary>
        private static readonly string[] CamposDestino = { "DESTINO", "EMPRESA", "CLIENTE" };

        /// <summary>
        /// Campos del encabezado donde puede estar la especie.
        ///
        /// Se buscan varias formas porque no todos los formatos la llaman
        /// igual: "ESPECIE" en la mayoría, "ESPECIE DECLARADA" en el PD-03.
        /// </summary>
        private static readonly string[] CamposEspecie =
            { "ESPECIE", "ESPECIE DECLARADA", "ESPECIE / PRESENTACION", "TIPO DE ESPECIE" };

        /// <summary>
        /// Especie del formulario, leída del encabezado.
        ///
        /// La comparación ignora tildes y mayúsculas: los formatos escriben
        /// "Especie", "ESPECIE" y "especie" indistintamente.
        /// </summary>
        private static string? EspecieDelFormulario(FilledForm form)
        {
            if (string.IsNullOrWhiteSpace(form.HeaderData)) return null;

            try
            {
                var header = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(form.HeaderData);
                if (header == null) return null;

                foreach (var buscado in CamposEspecie)
                {
                    foreach (var (clave, valor) in header)
                    {
                        if (!string.Equals(clave.Trim(), buscado, StringComparison.OrdinalIgnoreCase)) continue;
                        var texto = valor.ValueKind == JsonValueKind.String ? valor.GetString() : valor.ToString();
                        if (!string.IsNullOrWhiteSpace(texto)) return texto!.Trim();
                    }
                }

                // Respaldo: cualquier clave que contenga "ESPECIE". Cubre los
                // nombres que todavía no están en la lista de arriba.
                foreach (var (clave, valor) in header)
                {
                    if (!clave.ToUpperInvariant().Contains("ESPECIE")) continue;
                    var texto = valor.ValueKind == JsonValueKind.String ? valor.GetString() : valor.ToString();
                    // El PD-03 tiene "MARCACIÓN VISIBLE DE LA ESPECIE (SI/NO)":
                    // un sí o un no no es una especie.
                    if (string.IsNullOrWhiteSpace(texto)) continue;
                    var t = texto.Trim();
                    if (t.Equals("SI", StringComparison.OrdinalIgnoreCase)
                     || t.Equals("NO", StringComparison.OrdinalIgnoreCase)) continue;
                    return t;
                }
            }
            catch { /* HeaderData corrupto */ }

            return null;
        }

        /// <summary>Columna que dice la especie de forma explícita.</summary>
        private static readonly Regex RxColumnaEspecie = new(
            @"especie",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// "TIPO DE PRODUCTO" o "PRODUCTO". Anclado al inicio para no tomar
        /// "SUBPRODUCTO" ni "LIBRAS PRODUCTO TERMINADO".
        /// </summary>
        private static readonly Regex RxColumnaTipoProducto = new(
            @"^\s*(tipo\s*de\s*)?producto\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Especies conocidas. En las tablas el operario escribe el producto
        /// completo ("Sword steak s/p al vacio", "Mahi porcion s/p al vacio");
        /// esto lo lleva al nombre de la especie para que dos presentaciones de
        /// la misma especie no salgan como "Varios". El ORDEN importa: Blue
        /// Marlin antes que Picudo.
        /// Lo que no esté en la lista sale tal cual lo escribió el operario.
        /// </summary>
        private static readonly (Regex Rx, string Nombre)[] EspeciesConocidas =
        {
            (new Regex(@"sword|espada|xiphias", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Swordfish"),
            (new Regex(@"mahi|dorado|coryphaena", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Mahi Mahi"),
            (new Regex(@"wahoo|\bguaho\b|\bpeto\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Wahoo"),
            (new Regex(@"blue\s*marlin|marl[ií]n\s*azul", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Blue Marlin"),
            (new Regex(@"picudo", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Picudo"),
            (new Regex(@"\btuna\b|at[uú]n|albacora|yellowfin|bigeye|aleta\s*amarilla|patudo|skipjack|barrilete", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Tuna"),
            (new Regex(@"calamar|squid|\bpota\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Calamar"),
            (new Regex(@"camar[oó]n|shrimp|langostino", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Camarón"),
            (new Regex(@"king\s*clip|congrio", RegexOptions.IgnoreCase | RegexOptions.Compiled), "King Clip"),
            (new Regex(@"silver\s*sea\s*bass", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Silver Sea Bass"),
            (new Regex(@"robalo|snook", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Robalo"),
            (new Regex(@"corvina", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Corvina"),
            (new Regex(@"pargo|snapper", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Pargo"),
            (new Regex(@"\bmero\b|grouper", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Mero"),
            (new Regex(@"tilapia", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Tilapia"),
        };

        /// <summary>
        /// Donde empieza la presentación en un texto de producto:
        /// "Lenguado filete s/p al vacio" → "Lenguado".
        /// </summary>
        private static readonly Regex RxInicioPresentacion = new(
            @"\b(filete|fillet|porci[oó]n|portion|lomo|loin|steak|entero|whole|troncho|medall[oó]n|al\s*vac[ií]o)\b|\b[sc]\s*/\s*p\b|\d",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static string NormalizarEspecie(string texto)
        {
            var limpio = Regex.Replace(texto.Trim(), @"\s+", " ");
            foreach (var (rx, nombre) in EspeciesConocidas)
                if (rx.IsMatch(limpio)) return nombre;

            // Especie que no está en la lista: se quita la presentación para
            // que "X filete 1-3" y "X filete 3-5" cuenten como una sola.
            var m = RxInicioPresentacion.Match(limpio);
            if (m.Success && m.Index > 0)
            {
                var corto = limpio.Substring(0, m.Index).Trim(' ', '-', '/', ',');
                if (corto.Length > 0) return corto;
            }
            return limpio;
        }

        /// <summary>
        /// Especie leída de las TABLAS, para los formatos que no la tienen en el
        /// encabezado (PD-05, PD-06, PD-07, PD-11 la anotan por fila en
        /// "TIPO DE PRODUCTO"). Sin esto la columna salía vacía en todos ellos.
        ///
        /// Una sola especie → esa. Varias → "Varios", igual que en el reporte
        /// manual. Primero se busca en las tablas de producción; si ahí no hay
        /// nada, en cualquier tabla (la especie de la materia prima también vale).
        /// </summary>
        private static string? EspecieDeLasTablas(FilledForm form)
        {
            if (string.IsNullOrWhiteSpace(form.BodyData)) return null;

            try
            {
                using var doc = JsonDocument.Parse(form.BodyData);
                if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;

                var titulos = TitulosDeTablas(form);
                var roles = RolesDeColumnas(form.Template);

                foreach (var soloProduccion in new[] { true, false })
                {
                    var especies = new List<string>();

                    foreach (var el in doc.RootElement.EnumerateArray())
                    {
                        if (el.ValueKind != JsonValueKind.Object) continue;
                        if (!FilasDeTabla(el, out var filas)) continue;

                        var titulo = TituloDeTabla(el, titulos);
                        var tieneTitulo = !string.IsNullOrWhiteSpace(titulo);
                        if (soloProduccion && tieneTitulo
                            && (!RxTablaProduccion.IsMatch(titulo!) || RxMateriaPrima.IsMatch(titulo!)))
                            continue;

                        var filasObj = filas.EnumerateArray().Where(f => f.ValueKind == JsonValueKind.Object).ToList();
                        if (filasObj.Count == 0) continue;

                        // Qué columna usar en ESTA tabla: la marcada como producto,
                        // si no la que diga "especie", si no "tipo de producto".
                        var nombres = filasObj.SelectMany(f => f.EnumerateObject().Select(p => p.Name)).Distinct().ToList();
                        var columna =
                               nombres.FirstOrDefault(n => roles.TryGetValue(n.Trim(), out var r) && r == "producto")
                            ?? nombres.FirstOrDefault(n => RxColumnaEspecie.IsMatch(n))
                            ?? nombres.FirstOrDefault(n => RxColumnaTipoProducto.IsMatch(n));
                        if (columna == null) continue;

                        foreach (var fila in filasObj)
                        {
                            if (!fila.TryGetProperty(columna, out var v)) continue;
                            var texto = v.ValueKind == JsonValueKind.String ? v.GetString() : null;
                            if (string.IsNullOrWhiteSpace(texto)) continue;

                            var especie = NormalizarEspecie(texto!);
                            if (!especies.Any(e => string.Equals(e, especie, StringComparison.OrdinalIgnoreCase)))
                                especies.Add(especie);
                        }
                    }

                    if (especies.Count == 1) return especies[0];
                    if (especies.Count > 1) return "Varios";
                }
            }
            catch { /* BodyData corrupto */ }

            return null;
        }

        /// <summary>
        /// Campos del encabezado donde puede estar el lote del flujo.
        /// "LOTE DE PROCESO" primero: es el del producto que se está trabajando.
        /// </summary>
        private static readonly string[] CamposLote =
            { "LOTE DE PROCESO", "LOTE PROCESO", "LOTE", "LOTE MP", "N° LOTE", "NUMERO DE LOTE" };

        /// <summary>
        /// Lote del formulario, leído del encabezado.
        ///
        /// Es la clave del flujo: dos formularios del mismo lote son pasos del
        /// mismo recorrido, aunque se hayan cargado desordenados, aunque corran
        /// en paralelo o aunque haya una pausa entre ellos. Encadenar por horas
        /// consecutivas fallaba en los tres casos.
        /// </summary>
        private static string? LoteDelFormulario(FilledForm form)
        {
            if (string.IsNullOrWhiteSpace(form.HeaderData)) return null;

            try
            {
                var header = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(form.HeaderData);
                if (header == null) return null;

                foreach (var buscado in CamposLote)
                {
                    foreach (var (clave, valor) in header)
                    {
                        if (clave.ToUpperInvariant().Trim() != buscado) continue;
                        var texto = valor.ValueKind == JsonValueKind.String ? valor.GetString() : valor.ToString();
                        if (!string.IsNullOrWhiteSpace(texto)) return texto!.Trim();
                    }
                }
            }
            catch { /* HeaderData corrupto */ }

            return null;
        }

        /// <summary>
        /// A qué empresa va el proceso: Frigolab, San Mateo, Ecuatun.
        /// Sale del encabezado; vacío si el formulario no lo declara.
        /// </summary>
        private static string? DestinoDelFormulario(FilledForm form)
        {
            if (string.IsNullOrWhiteSpace(form.HeaderData)) return null;

            try
            {
                var header = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(form.HeaderData);
                if (header == null) return null;

                foreach (var buscado in CamposDestino)
                {
                    foreach (var (clave, valor) in header)
                    {
                        if (clave.ToUpperInvariant().Trim() != buscado) continue;
                        var texto = valor.ValueKind == JsonValueKind.String ? valor.GetString() : valor.ToString();
                        if (!string.IsNullOrWhiteSpace(texto)) return texto!.Trim();
                    }
                }
            }
            catch { /* HeaderData corrupto: se devuelve vacío */ }

            return null;
        }

                /// <summary>Columnas de tabla donde el operario anota el proceso.</summary>
        private static readonly Regex RxColumnaProceso = new(
            @"^(tipo\s*de\s*proceso|proceso(\s*productivo)?|actividad(\s*de\s*proceso)?)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Procesos anotados en las TABLAS del formulario.
        ///
        /// El operario puede registrar el proceso en el encabezado —cuando el
        /// formulario entero es de una sola actividad— o fila por fila en la
        /// tabla, cuando en el mismo registro hizo varias cosas. Leer solo el
        /// encabezado dejaba la columna vacía en el segundo caso.
        /// </summary>
        private static List<string> ProcesosDeLasTablas(FilledForm form)
        {
            var vistos = new List<string>();
            if (string.IsNullOrWhiteSpace(form.BodyData)) return vistos;

            try
            {
                using var doc = JsonDocument.Parse(form.BodyData);
                if (doc.RootElement.ValueKind != JsonValueKind.Array) return vistos;

                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    if (el.ValueKind != JsonValueKind.Object) continue;
                    if (!FilasDeTabla(el, out var filas)) continue;

                    foreach (var fila in filas.EnumerateArray())
                    {
                        if (fila.ValueKind != JsonValueKind.Object) continue;
                        foreach (var prop in fila.EnumerateObject())
                        {
                            if (!RxColumnaProceso.IsMatch(prop.Name.Trim())) continue;

                            var texto = prop.Value.ValueKind == JsonValueKind.String
                                ? prop.Value.GetString()
                                : prop.Value.ToString();
                            if (string.IsNullOrWhiteSpace(texto)) continue;

                            var limpio = Regex.Replace(texto.Trim(), @"\s+", " ");
                            if (PlaceholdersSupervisa.Contains(limpio)) continue;
                            if (!vistos.Any(v => string.Equals(v, limpio, StringComparison.OrdinalIgnoreCase)))
                                vistos.Add(limpio);
                        }
                    }
                }
            }
            catch { /* BodyData corrupto */ }

            return vistos;
        }

                /// <summary>
        /// Proceso productivo del formulario, COMBINANDO todas las fuentes.
        ///
        /// El dato vive en tres lugares según el formato:
        ///   · el encabezado (TIPO DE PROCESO), cuando el registro entero es de
        ///     una sola actividad;
        ///   · una columna de la tabla, cuando en el mismo registro se hicieron
        ///     varias cosas;
        ///   · la plantilla ("Proceso - Productivo"), que declara lo que ese
        ///     formato cubre.
        ///
        /// Se combinan las dos primeras —encabezado y tabla— porque las dos son
        /// lo que el operario REGISTRÓ: un turno puede tocar varios procesos y
        /// quedarse con uno solo escondería la mitad del trabajo.
        ///
        /// La de la plantilla NO se suma: solo se usa si no se registró nada.
        /// Decir lo que el formato "puede cubrir" junto a lo que pasó de verdad
        /// mezcla un dato con una declaración, y el resultado es falso —
        /// PRUEBA-02 declara "Congelado" y el operario hizo "Descongelar".
        ///
        /// Se deduplica sin distinguir mayúsculas ni tildes: "Corte" y "corte"
        /// son el mismo proceso y repetirlos solo hace ruido.
        /// </summary>
        private static string ProcesoDelFormulario(FilledForm form)
        {
            var procesos = new List<string>();

            // Clave de comparación sin tildes ni mayúsculas: "Clasificación" y
            // "Clasificacion" se escriben de las dos formas en planta y son el
            // mismo proceso.
            static string Clave(string v) => new string(
                v.Normalize(System.Text.NormalizationForm.FormD)
                 .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                             != System.Globalization.UnicodeCategory.NonSpacingMark)
                 .ToArray()).ToLowerInvariant().Trim();

            var claves = new HashSet<string>();

            void Agregar(string? v)
            {
                if (string.IsNullOrWhiteSpace(v)) return;
                var limpio = Regex.Replace(v.Trim(), @"\s+", " ");
                if (limpio.Length == 0) return;
                if (PlaceholdersSupervisa.Contains(limpio)) return;
                if (claves.Add(Clave(limpio))) procesos.Add(limpio);
            }

            // 1. El encabezado
            if (!string.IsNullOrWhiteSpace(form.HeaderData))
            {
                try
                {
                    var header = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(form.HeaderData);
                    if (header != null)
                    {
                        foreach (var buscado in CamposProceso)
                        {
                            foreach (var (clave, valor) in header)
                            {
                                var k = clave.ToUpperInvariant().Trim();
                                if (k != buscado || k.Contains("LOTE")) continue;
                                Agregar(valor.ValueKind == JsonValueKind.String ? valor.GetString() : valor.ToString());
                            }
                        }
                    }
                }
                catch { /* HeaderData corrupto */ }
            }

            // 2. Las tablas
            foreach (var p in ProcesosDeLasTablas(form)) Agregar(p);

            // Si el operario registró algo, ESO es el proceso del turno.
            //
            // Lo que declara la plantilla en «Proceso - Productivo» NO se suma
            // acá a propósito. Son cosas distintas: la plantilla dice lo que el
            // formato PUEDE cubrir, el registro dice lo que SE HIZO.
            //
            // Sumarlos daba resultados falsos: PRUEBA-02 declara "Congelado" y
            // el operario eligió "Descongelar", así que salía
            // "Descongelar · Congelado" como si el turno hubiera tenido los dos
            // procesos. Solo tuvo uno.
            if (procesos.Count > 0) return string.Join(" · ", procesos);

            // 3. Nada registrado: recién ahí sirve lo que declara la plantilla,
            //    como mejor aproximación disponible.
            var declaradas = ActividadesDePlantilla(form.Template);
            if (!string.IsNullOrWhiteSpace(declaradas)) return declaradas;

            return form.Template?.Proceso ?? form.Template?.Area ?? "Sin proceso";
        }

                private const double MaxHorasJornada = 18;

        private static decimal? CalcularHoras(TimeSpan? inicio, TimeSpan? fin)
        {
            if (!inicio.HasValue || !fin.HasValue) return null;

            var horas = (fin.Value - inicio.Value).TotalHours;

            // Cruza medianoche: el fin cayó al día siguiente.
            if (horas < 0) horas += 24;

            if (horas <= 0 || horas > MaxHorasJornada) return null;

            return (decimal)horas;
        }

                private static DateTime FinDelDia(DateTime fecha) => fecha.Date.AddDays(1).AddTicks(-1);

        /// <summary>Inicio del día: descarta la hora que venga en el parámetro.</summary>
        private static DateTime InicioDelDia(DateTime fecha) => fecha.Date;

        private readonly ApplicationDbContext _context;
        private readonly PersonalValidationService _validador;
        private readonly ILogger<PersonalController> _logger;

        public PersonalController(
            ApplicationDbContext context,
            PersonalValidationService validador,
            ILogger<PersonalController> logger)
        {
            _context = context;
            _validador = validador;
            _logger = logger;
        }

        // ── GET /api/Personal ────────────────────────────────────────────────
        /// <summary>
        /// Lista registros de personal. Filtros opcionales:
        /// ?proceso=Fileteo&desde=2026-01-01&hasta=2026-12-31&cumpleEstandar=false
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<PersonalRegistro>>> GetAll(
            [FromQuery] string? proceso,
            [FromQuery] DateTime? desde,
            [FromQuery] DateTime? hasta,
            [FromQuery] bool? cumpleEstandar)
        {
            try
            {
                var query = _context.PersonalRegistros.AsQueryable();

                if (!string.IsNullOrWhiteSpace(proceso))
                    query = query.Where(p => p.Proceso.Contains(proceso));

                if (desde.HasValue)
                    query = query.Where(p => p.Fecha >= InicioDelDia(desde.Value));

                if (hasta.HasValue)
                    query = query.Where(p => p.Fecha <= FinDelDia(hasta.Value));

                if (cumpleEstandar.HasValue)
                    query = query.Where(p => p.CumpleEstandar == cumpleEstandar.Value);

                var registros = await query
                    .OrderByDescending(p => p.Fecha)
                    .ThenByDescending(p => p.CreadoEn)
                    .ToListAsync();

                return Ok(registros);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al listar registros de Personal");
                return StatusCode(500, new { message = "Error al obtener registros de personal" });
            }
        }

        // ── GET /api/Personal/{id} ───────────────────────────────────────────
        [HttpGet("{id:int}")]
        public async Task<ActionResult<PersonalRegistro>> GetById(int id)
        {
            var registro = await _context.PersonalRegistros.FindAsync(id);
            if (registro == null)
                return NotFound(new { message = "Registro de personal no encontrado" });

            return Ok(registro);
        }

        // ── GET /api/Personal/incumplimientos ────────────────────────────────
        /// <summary>Solo registros que no cumplen el estándar, para revisión de costos.</summary>
        [HttpGet("incumplimientos")]
        public async Task<ActionResult<IEnumerable<PersonalRegistro>>> GetIncumplimientos(
            [FromQuery] DateTime? desde,
            [FromQuery] DateTime? hasta)
        {
            var query = _context.PersonalRegistros.Where(p => !p.CumpleEstandar);

            if (desde.HasValue)
                query = query.Where(p => p.Fecha >= InicioDelDia(desde.Value));
            if (hasta.HasValue)
                query = query.Where(p => p.Fecha <= FinDelDia(hasta.Value));

            var registros = await query
                .OrderByDescending(p => p.Fecha)
                .ToListAsync();

            return Ok(registros);
        }

        // ── GET /api/Personal/indicadores ────────────────────────────────────
        /// <summary>
        /// Totales agregados por proceso y fecha (horas-hombre, planta vs externo).
        /// La agregación se hace en SQL (GroupBy -> GROUP BY) para no traer todo
        /// a memoria, igual que el resto de indicadores de costos del sistema.
        /// </summary>
        [HttpGet("indicadores")]
        public async Task<ActionResult<IEnumerable<PersonalIndicadorDto>>> GetIndicadores(
            [FromQuery] DateTime? desde,
            [FromQuery] DateTime? hasta,
            [FromQuery] string? proceso)
        {
            var query = _context.PersonalRegistros.AsQueryable();

            if (desde.HasValue)
                query = query.Where(p => p.Fecha >= InicioDelDia(desde.Value));
            if (hasta.HasValue)
                query = query.Where(p => p.Fecha <= FinDelDia(hasta.Value));
            if (!string.IsNullOrWhiteSpace(proceso))
                query = query.Where(p => p.Proceso.Contains(proceso));

            var indicadores = await query
                .GroupBy(p => new { p.Proceso, p.Fecha })
                .Select(g => new PersonalIndicadorDto
                {
                    Proceso = g.Key.Proceso,
                    Fecha = g.Key.Fecha,
                    TotalPlanta = g.Sum(x => x.PersonalPlanta),
                    TotalExterno = g.Sum(x => x.PersonalExterno),
                    TotalHoras = g.Sum(x => x.HorasTrabajadas),
                    Registros = g.Count(),
                    Incumplimientos = g.Count(x => !x.CumpleEstandar)
                })
                .OrderByDescending(i => i.Fecha)
                .ThenBy(i => i.Proceso)
                .ToListAsync();

            return Ok(indicadores);
        }

        // ── GET /api/Personal/extraidos ──────────────────────────────────────
        /// <summary>
        /// Extrae y agrega el "Control del Personal" directamente desde la tabla
        /// dinámica dentro de los formularios llenados (columnas HORA INICIO,
        /// HORA FIN, PERSONAL PLANTA, PERSONAL EXTERNO, OBSERVACIÓN), sin
        /// requerir doble digitación manual: suma el personal de todas las filas
        /// de cada formulario y calcula la hora de inicio (mínima) y fin (máxima)
        /// real del formulario. Valida contra el estándar configurado por
        /// formulario (o por proceso como respaldo) y agrega la advertencia al
        /// campo Observaciones cuando el horario se sale del estándar.
        /// </summary>
        [HttpGet("extraidos")]
        public async Task<ActionResult<IEnumerable<PersonalExtraidoDto>>> GetExtraidos(
            [FromQuery] DateTime? desde,
            [FromQuery] DateTime? hasta,
            [FromQuery] int? templateId,
            [FromQuery] string? proceso)
        {
            try
            {
                var query = _context.FilledForms.Include(f => f.Template).AsQueryable();

                // Filtro exacto en SQL: FechaRegistro es una columna indexada
                // con la fecha del encabezado. Antes había que traer un rango
                // holgado de ±7 días y recortar en memoria, porque el dato vivía
                // dentro del JSON de HeaderData y SQL no lo podía leer.
                if (desde.HasValue)
                    query = query.Where(f => f.FechaRegistro >= desde.Value.Date);
                if (hasta.HasValue)
                    query = query.Where(f => f.FechaRegistro <= hasta.Value.Date);
                if (templateId.HasValue)
                    query = query.Where(f => f.TemplateID == templateId.Value);

                var forms = await query.OrderByDescending(f => f.CreatedAt).ToListAsync();
                var estandares = await _context.ProcesoEstandares.Where(e => e.Activo).ToListAsync();

                // Alertas ya generadas para no duplicar cada vez que el dashboard
                // vuelve a pedir /extraidos (se refresca periódicamente).
                var formIds = forms.Select(f => f.FormID).ToList();
                var formIdsConAlerta = (await _context.Alerts
                    .Where(a => a.Type == "personal_tiempo_excedido" && a.FormId != null && formIds.Contains(a.FormId.Value))
                    .Select(a => a.FormId!.Value)
                    .ToListAsync())
                    .ToHashSet();
                var alertasNuevas = new List<Alert>();

                var resultado = new List<PersonalExtraidoDto>();
                foreach (var form in forms)
                {
                    // Un formulario puede dar VARIAS filas, una por franja horaria.
                    foreach (var extraido in ExtraerControlPersonal(form))
                    {
                        if (!string.IsNullOrWhiteSpace(proceso) &&
                            !extraido.Proceso.Contains(proceso, StringComparison.OrdinalIgnoreCase))
                            continue;

                        var estandar = estandares.FirstOrDefault(e => e.TemplateID == form.TemplateID)
                            ?? estandares.FirstOrDefault(e => e.TemplateID == null && e.Proceso == extraido.Proceso);

                        // Único criterio configurable: tiempo estimado (horas) por formulario.
                        if (estandar?.DuracionMaximaHoras is decimal estimado && extraido.HorasTrabajadas is decimal horas && horas > estimado)
                        {
                            extraido.CumpleEstandar = false;
                            var advertencia = $"⏱ Tiempo estimado superado: {horas:0.##}h trabajadas (estimado {estimado:0.##}h)";
                            extraido.Observaciones = string.IsNullOrWhiteSpace(extraido.Observaciones)
                                ? advertencia
                                : $"{extraido.Observaciones} | {advertencia}";

                            // Una sola alerta por formulario, aunque tenga varias
                            // franjas excedidas: si no, un registro con cuatro
                            // tramos generaría cuatro correos del mismo problema.
                            if (formIdsConAlerta.Add(form.FormID))
                            {
                                alertasNuevas.Add(new Alert
                                {
                                    Type = "personal_tiempo_excedido",
                                    Priority = "medium",
                                    Title = $"Tiempo estimado superado en {extraido.Formulario}",
                                    Message = $"{extraido.Proceso}: {horas:0.##}h trabajadas, estimado {estimado:0.##}h.",
                                    TargetEmail = string.Empty,
                                    FormId = form.FormID,
                                    CreatedDate = DateTime.Now,
                                    Status = "pending"
                                });
                            }
                        }

                        resultado.Add(extraido);
                    }
                }

                if (alertasNuevas.Count > 0)
                {
                    _context.Alerts.AddRange(alertasNuevas);
                    await _context.SaveChangesAsync();
                }

                // Se ordena por la fecha que se MUESTRA, no por la de guardado:
                // como ahora sale del encabezado, ordenar por CreatedAt dejaba
                // las filas desordenadas en pantalla. Dentro del mismo día manda
                // la hora de inicio, que es como se lee una jornada.
                // Orden pensado para leer la jornada como FLUJOS.
                //
                // Cada formulario es un paso del proceso: recepción de 07 a 08,
                // fileteo de 08 a 10, empaque de 10 a 16. Ordenar por formato
                // los separaba —todos los PD-04 juntos, todos los PD-07 juntos—
                // y la cadena quedaba rota.
                //
                // Día → destino → HORA. Así los pasos salen en el orden en que
                // ocurrieron y la continuidad horaria se ve sola: el que termina
                // a las 10 queda pegado al que arranca a las 10.
                // El LOTE es la clave del flujo: agrupa los pasos del mismo
                // recorrido aunque se hayan cargado desordenados o corran en
                // paralelo. Dentro del lote, por hora, que es el orden en que
                // ocurrieron.
                //
                // Los formularios sin lote quedan agrupados por destino, que es
                // lo único que los relaciona.
                return Ok(resultado
                    .OrderByDescending(r => r.Fecha)
                    .ThenBy(r => r.Destino ?? "")
                    .ThenBy(r => string.IsNullOrWhiteSpace(r.Lote) ? "zzz" : r.Lote)
                    // Dentro del mismo formulario, las franjas en orden
                    // cronológico: 07-08 antes que 08-16.
                    .ThenBy(r => r.FormID)
                    .ThenBy(r => r.HoraInicio ?? TimeSpan.MaxValue)
                    .ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al extraer registros de Control de Personal");
                return StatusCode(500, new { message = "Error al extraer registros de control de personal" });
            }
        }

        // ── POST /api/Personal ───────────────────────────────────────────────
        /// <summary>
        /// Crea un registro de personal. Si el horario/duración se sale del
        /// estándar configurado para el proceso, exige JustificacionVariacion
        /// y genera una alerta (reutiliza la tabla Alert existente).
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<PersonalRegistro>> Create([FromBody] PersonalRegistroCreateDto dto)
        {
            try
            {
                if (dto.HoraHasta <= dto.HoraDesde)
                    return BadRequest(new { message = "La hora 'hasta' debe ser posterior a la hora 'desde'." });

                var estandar = await _context.ProcesoEstandares
                    .FirstOrDefaultAsync(e => e.Proceso == dto.Proceso && e.Activo);

                var resultado = _validador.Validar(dto.HoraDesde, dto.HoraHasta, estandar);

                if (resultado.RequiereJustificacion && string.IsNullOrWhiteSpace(dto.JustificacionVariacion))
                {
                    return BadRequest(new
                    {
                        error = "justificacion_requerida",
                        motivo = resultado.Motivo,
                        message = "El horario registrado se sale del estándar del proceso. Debe indicar una observación justificando la variación."
                    });
                }

                var registro = new PersonalRegistro
                {
                    FormID = dto.FormID,
                    TemplateId = dto.TemplateId,
                    Proceso = dto.Proceso,
                    Fecha = dto.Fecha.Date,
                    PersonalPlanta = dto.PersonalPlanta,
                    PersonalExterno = dto.PersonalExterno,
                    HoraDesde = dto.HoraDesde,
                    HoraHasta = dto.HoraHasta,
                    HorasTrabajadas = (decimal)(dto.HoraHasta - dto.HoraDesde).TotalHours,
                    Observaciones = dto.Observaciones,
                    CumpleEstandar = resultado.CumpleEstandar,
                    MotivoIncumplimiento = resultado.Motivo,
                    JustificacionVariacion = dto.JustificacionVariacion,
                    CreadoPor = User?.Identity?.Name
                };

                _context.PersonalRegistros.Add(registro);
                await _context.SaveChangesAsync();

                if (!resultado.CumpleEstandar)
                {
                    _context.Alerts.Add(new Alert
                    {
                        Type = "personal_fuera_estandar",
                        Priority = "medium",
                        Title = $"Variación de horario en {dto.Proceso}",
                        Message = $"Motivo: {resultado.Motivo}. Justificación: {dto.JustificacionVariacion}",
                        TargetEmail = string.Empty,
                        FormId = dto.FormID,
                        CreatedDate = DateTime.Now,
                        Status = "pending"
                    });
                    await _context.SaveChangesAsync();
                }

                return CreatedAtAction(nameof(GetById), new { id = registro.Id }, registro);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear registro de Personal");
                return StatusCode(500, new { message = "Error al crear el registro de personal" });
            }
        }

        // ── PUT /api/Personal/{id} ───────────────────────────────────────────
        [HttpPut("{id:int}")]
        public async Task<ActionResult<PersonalRegistro>> Update(int id, [FromBody] PersonalRegistroUpdateDto dto)
        {
            var registro = await _context.PersonalRegistros.FindAsync(id);
            if (registro == null)
                return NotFound(new { message = "Registro de personal no encontrado" });

            if (dto.HoraHasta <= dto.HoraDesde)
                return BadRequest(new { message = "La hora 'hasta' debe ser posterior a la hora 'desde'." });

            var estandar = await _context.ProcesoEstandares
                .FirstOrDefaultAsync(e => e.Proceso == registro.Proceso && e.Activo);

            var resultado = _validador.Validar(dto.HoraDesde, dto.HoraHasta, estandar);

            if (resultado.RequiereJustificacion && string.IsNullOrWhiteSpace(dto.JustificacionVariacion))
            {
                return BadRequest(new
                {
                    error = "justificacion_requerida",
                    motivo = resultado.Motivo,
                    message = "El horario registrado se sale del estándar del proceso. Debe indicar una observación justificando la variación."
                });
            }

            registro.PersonalPlanta = dto.PersonalPlanta;
            registro.PersonalExterno = dto.PersonalExterno;
            registro.HoraDesde = dto.HoraDesde;
            registro.HoraHasta = dto.HoraHasta;
            registro.HorasTrabajadas = (decimal)(dto.HoraHasta - dto.HoraDesde).TotalHours;
            registro.Observaciones = dto.Observaciones;
            registro.CumpleEstandar = resultado.CumpleEstandar;
            registro.MotivoIncumplimiento = resultado.Motivo;
            registro.JustificacionVariacion = dto.JustificacionVariacion;
            registro.ActualizadoEn = DateTime.Now;

            await _context.SaveChangesAsync();
            return Ok(registro);
        }

        // ── DELETE /api/Personal/{id} ────────────────────────────────────────
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var registro = await _context.PersonalRegistros.FindAsync(id);
            if (registro == null)
                return NotFound(new { message = "Registro de personal no encontrado" });

            _context.PersonalRegistros.Remove(registro);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // ── Estándares de proceso (mantenimiento de reglas) ──────────────────

        // GET /api/Personal/estandares
        [HttpGet("estandares")]
        public async Task<ActionResult<IEnumerable<ProcesoEstandar>>> GetEstandares()
        {
            return Ok(await _context.ProcesoEstandares.OrderBy(e => e.FormularioNombre ?? e.Proceso).ToListAsync());
        }

        // POST /api/Personal/estandares
        [HttpPost("estandares")]
        public async Task<ActionResult<ProcesoEstandar>> CreateEstandar([FromBody] ProcesoEstandarCreateDto dto)
        {
            string? nombreFormulario = null;
            if (dto.TemplateID.HasValue)
            {
                var plantilla = await _context.Templates.FindAsync(dto.TemplateID.Value);
                nombreFormulario = plantilla?.Nombre;
            }

            var estandar = new ProcesoEstandar
            {
                TemplateID = dto.TemplateID,
                FormularioNombre = nombreFormulario,
                Proceso = dto.Proceso,
                HoraInicioEsperada = dto.HoraInicioEsperada,
                HoraFinEsperada = dto.HoraFinEsperada,
                DuracionMinimaHoras = dto.DuracionMinimaHoras,
                DuracionMaximaHoras = dto.DuracionMaximaHoras,
                ToleranciaMinutos = dto.ToleranciaMinutos,
                Activo = dto.Activo
            };

            _context.ProcesoEstandares.Add(estandar);
            await _context.SaveChangesAsync();
            return Ok(estandar);
        }

        // PUT /api/Personal/estandares/{id}
        [HttpPut("estandares/{id:int}")]
        public async Task<ActionResult<ProcesoEstandar>> UpdateEstandar(int id, [FromBody] ProcesoEstandarCreateDto dto)
        {
            var estandar = await _context.ProcesoEstandares.FindAsync(id);
            if (estandar == null)
                return NotFound(new { message = "Estándar de proceso no encontrado" });

            string? nombreFormulario = estandar.FormularioNombre;
            if (dto.TemplateID.HasValue && dto.TemplateID != estandar.TemplateID)
            {
                var plantilla = await _context.Templates.FindAsync(dto.TemplateID.Value);
                nombreFormulario = plantilla?.Nombre;
            }
            else if (!dto.TemplateID.HasValue)
            {
                nombreFormulario = null;
            }

            estandar.TemplateID = dto.TemplateID;
            estandar.FormularioNombre = nombreFormulario;
            estandar.Proceso = dto.Proceso;
            estandar.HoraInicioEsperada = dto.HoraInicioEsperada;
            estandar.HoraFinEsperada = dto.HoraFinEsperada;
            estandar.DuracionMinimaHoras = dto.DuracionMinimaHoras;
            estandar.DuracionMaximaHoras = dto.DuracionMaximaHoras;
            estandar.ToleranciaMinutos = dto.ToleranciaMinutos;
            estandar.Activo = dto.Activo;
            estandar.ActualizadoEn = DateTime.Now;

            await _context.SaveChangesAsync();
            return Ok(estandar);
        }

        // DELETE /api/Personal/estandares/{id}
        [HttpDelete("estandares/{id:int}")]
        public async Task<IActionResult> DeleteEstandar(int id)
        {
            var estandar = await _context.ProcesoEstandares.FindAsync(id);
            if (estandar == null)
                return NotFound(new { message = "Estándar de proceso no encontrado" });

            _context.ProcesoEstandares.Remove(estandar);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // ── Extracción de "Control del Personal" desde FilledForm.BodyData ──

        private static readonly Regex RxHoraInicio = new(@"HORA\s*INICIO", RegexOptions.IgnoreCase);
        private static readonly Regex RxHoraFin = new(@"HORA\s*FIN", RegexOptions.IgnoreCase);
        /// <summary>
        /// Rol declarado de cada columna, leído de la PLANTILLA.
        ///
        /// Devuelve un diccionario {nombre de columna → rol}. El rol lo marca
        /// quien edita la plantilla en «Qué representa», y manda sobre
        /// cualquier deducción por el nombre.
        ///
        /// Antes el sistema adivinaba: buscaba "PLANTA" o "EXTERNO" en el
        /// título. Cada vez que alguien creaba una columna con un nombre nuevo
        /// —"N° Personas Frigolab", "Cuadrilla"— esa columna dejaba de contarse
        /// y nadie se enteraba hasta que un reporte salía en cero.
        ///
        /// Con la marca, el nombre de la columna puede ser cualquiera.
        /// </summary>
        private static Dictionary<string, string> RolesDeColumnas(Template? template)
        {
            var roles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(template?.BodyElements)) return roles;

            try
            {
                using var doc = JsonDocument.Parse(template!.BodyElements!);
                if (doc.RootElement.ValueKind != JsonValueKind.Array) return roles;

                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    if (el.ValueKind != JsonValueKind.Object) continue;
                    if (!el.TryGetProperty("columns", out var cols) || cols.ValueKind != JsonValueKind.Array) continue;

                    foreach (var c in cols.EnumerateArray())
                    {
                        if (c.ValueKind != JsonValueKind.Object) continue;

                        var rol = c.TryGetProperty("rol", out var r) ? r.GetString() : null;
                        if (string.IsNullOrWhiteSpace(rol)) continue;

                        var label = c.TryGetProperty("label", out var l) ? l.GetString() : null;
                        if (string.IsNullOrWhiteSpace(label)) continue;

                        roles[label!.Trim()] = rol!.Trim();
                    }
                }
            }
            catch { /* BodyElements corrupto: se cae a la detección por nombre */ }

            return roles;
        }

                // ── Personal propio vs contratado ────────────────────────────────
        //
        // Cada formato lo nombra distinto y con solo "PLANTA" y "EXTERNO" se
        // perdían tres columnas enteras: el PD-20 usa "N° Personas Frigolab" y
        // "N° Personas Eventual/Cuadrilla", el PD-21 "N° PERSONAL
        // CUADRILLA/EVENTUAL". Esas columnas no sumaban en ningún lado y el
        // personal salía en cero.
        //
        // ⚠️ El ORDEN de evaluación importa: "N° PERSONAL EVENTUAL/EXTERNO"
        // contiene las dos ideas. Se revisa primero si es EVENTUAL, porque esa
        // palabra es la que define de qué personal se trata; PLANTA queda para
        // las que no lo son.

        /// <summary>Personal contratado: eventual, cuadrilla, externo.</summary>
        private static readonly Regex RxExterno = new(
            @"EVENTUAL|CUADRILLA|EXTERNO|CONTRATAD|TERCERO",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>Personal propio de la empresa.</summary>
        private static readonly Regex RxPlanta = new(
            @"PLANTA|FRIGOLAB|PROPIO|FIJO|N[OÓ]MINA",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex RxObs = new(@"OBSERVA", RegexOptions.IgnoreCase);

        /// <summary>Columna con las casillas de Inicio / Cierre del flujo.</summary>
        private static readonly Regex RxInicioCierre = new(
            @"inicio\s*/?\s*cierre|check|marca\s*de\s*flujo",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Busca dentro de BodyData una tabla din\u00e1mica de "Control del Personal"
        /// (detectada por sus columnas, no por un t\u00edtulo fijo, para que funcione
        /// igual sin importar en qu\u00e9 formulario est\u00e9 embebida): suma PERSONAL
        /// PLANTA/EXTERNO de todas las filas y calcula la HORA INICIO m\u00ednima y
        /// HORA FIN m\u00e1xima del formulario. Devuelve null si el formulario no
        /// contiene ninguna tabla de este tipo.
        /// </summary>
        /// <summary>
        /// Extrae los registros de personal de un formulario.
        ///
        /// Devuelve UNA FILA POR FRANJA HORARIA, no una por formulario: un
        /// registro con 07-08 y 08-16 da dos líneas, cada una con su personal y
        /// su peso. Antes se sumaba todo y se perdía el detalle.
        /// </summary>
        private List<PersonalExtraidoDto> ExtraerControlPersonal(FilledForm form)
        {
            if (string.IsNullOrEmpty(form.BodyData))
                return null;

            JsonElement formData;
            try
            {
                formData = JsonSerializer.Deserialize<JsonElement>(form.BodyData);
            }
            catch
            {
                return null;
            }

            if (formData.ValueKind != JsonValueKind.Array)
                return null;

            // ── UNA FILA POR FRANJA HORARIA ──────────────────────────────
            //
            // Antes se sumaba todo el personal del formulario y las horas se
            // colapsaban en el mínimo de inicio y el máximo de fin: un registro
            // con 07-08 (4 personas) y 08-16 (12 personas) salía como una sola
            // línea 07:00-16:00 con 16 personas.
            //
            // Eso escondía el detalle que se necesita para costos: cuánta gente
            // hubo en cada tramo y cuánto se produjo en cada uno. Ahora cada
            // fila de la tabla de personal es su propio registro.
            var filasPersonal = new List<(TimeSpan? Ini, TimeSpan? Fin, int Planta, int Externo, decimal? Peso, string? Obs, string? Proceso, bool EsInicio, bool EsCierre)>();
            bool encontrado = false;
            var estructura = ColumnasDeTablas(form);
            var posicion = -1;

            foreach (var element in formData.EnumerateArray())
            {
                posicion++;
                if (element.ValueKind != JsonValueKind.Object)
                    continue;

                if (!element.TryGetProperty("type", out var typeEl) ||
                    typeEl.ValueKind != JsonValueKind.String ||
                    typeEl.GetString() != "table")
                    continue;

                if (!FilasDeTabla(element, out var dataArr))
                    continue;

                var filas = dataArr.EnumerateArray().Where(r => r.ValueKind == JsonValueKind.Object).ToList();
                if (filas.Count == 0)
                    continue;

                var columnas = filas[0].EnumerateObject().Select(p => p.Name).ToList();

                // Roles declarados en la plantilla. Mandan sobre el nombre.
                var roles = RolesDeColumnas(form.Template);
                string? RolDe(string col) => roles.TryGetValue(col.Trim(), out var r) ? r : null;
                bool tieneHorario =
                       columnas.Any(c => RolDe(c) is "horaInicio" or "horaFin")
                    || columnas.Any(c => RxHoraInicio.IsMatch(c))
                    || columnas.Any(c => RxHoraFin.IsMatch(c));
                // Una tabla sirve si tiene columnas de personal, sea por su
                // marca o por su nombre.
                bool tienePersonal =
                       columnas.Any(c => RolDe(c) is "personalPropio" or "personalContratado")
                    || columnas.Any(c => RxPlanta.IsMatch(c))
                    || columnas.Any(c => RxExterno.IsMatch(c));
                if (!tieneHorario || !tienePersonal)
                    continue;

                encontrado = true;

                // ⚖️ LIBRAS NETAS EN 0 EN EL REPORTE DE PERSONAL
                // La columna calculada (ej. LIBRAS NETAS = PESO PT o CAJAS ×
                // CAPACIDAD) quedó guardada en 0 en muchos registros: la pantalla
                // la calcula al dibujar, pero no siempre la guardaba. Aquí se
                // calcula con la fórmula de la plantilla cuando está en 0, y las
                // columnas de peso que la alimentan (PESO PT) no se suman aparte,
                // porque eso contaba el mismo peso dos veces.
                var plan = PlanDePeso(element, posicion, estructura);

                foreach (var fila in filas)
                {
                    int planta = 0, externo = 0;
                    TimeSpan? ini = null, fin = null;
                    decimal? peso = null;
                    string? obs = null, proc = null;
                    bool esInicio = false, esCierre = false;
                    var textos = plan.TieneFormulas ? TextosDeFila(fila) : null;

                    foreach (var prop in fila.EnumerateObject())
                    {
                        var nombre = prop.Name;

                        // ── 0º: peso calculado y sus entradas ─────────────
                        if (textos != null)
                        {
                            if (plan.EsEntrada(nombre)) continue;
                            if (plan.FormulaDe(nombre) != null)
                            {
                                var vf = plan.PesoCalculado(nombre, ParseDecimal(prop.Value), textos);
                                if (vf > 0) peso = (peso ?? 0) + vf;
                                continue;
                            }
                        }

                        // ── 1º: el rol declarado en la plantilla ──────────
                        var rol = RolDe(nombre);
                        if (rol != null)
                        {
                            switch (rol)
                            {
                                case "personalPropio":      planta  += ParseEntero(prop.Value); continue;
                                case "personalContratado":  externo += ParseEntero(prop.Value); continue;
                                case "horaInicio":          ini = ParseHora(prop.Value) ?? ini; continue;
                                case "horaFin":             fin = ParseHora(prop.Value) ?? fin; continue;
                                case "pesoProducido":
                                {
                                    var vr = ParseDecimal(prop.Value);
                                    if (vr > 0) peso = (peso ?? 0) + vr;
                                    continue;
                                }
                                case "procesoProductivo":
                                {
                                    var tr = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : null;
                                    if (!string.IsNullOrWhiteSpace(tr)) proc = tr!.Trim();
                                    continue;
                                }
                                case "observacion":
                                {
                                    var tr = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : null;
                                    if (!string.IsNullOrWhiteSpace(tr)) obs = tr!.Trim();
                                    continue;
                                }
                                // pesoMateriaPrima, producto e ignorar: no entran
                                // en el reporte de personal. Marcarlas evita que
                                // la detección por nombre las tome por error.
                                case "pesoMateriaPrima":
                                case "producto":
                                case "ignorar":
                                    continue;
                            }
                        }

                        // ── 2º: sin marca, se deduce por el nombre ────────
                        // EVENTUAL primero: "N° PERSONAL EVENTUAL/EXTERNO"
                        // contiene las dos ideas, y esa palabra es la que define
                        // de qué personal se trata.
                        if (RxExterno.IsMatch(nombre))
                            externo += ParseEntero(prop.Value);
                        else if (RxPlanta.IsMatch(nombre))
                            planta += ParseEntero(prop.Value);
                        else if (RxHoraInicio.IsMatch(nombre))
                            ini = ParseHora(prop.Value) ?? ini;
                        else if (RxHoraFin.IsMatch(nombre))
                            fin = ParseHora(prop.Value) ?? fin;
                        else if (RxPesoProd.IsMatch(nombre) && !RxNoEsPeso.IsMatch(nombre))
                        {
                            var v = ParseDecimal(prop.Value);
                            if (v > 0) peso = (peso ?? 0) + v;
                        }
                        else if (RxColumnaProceso.IsMatch(nombre.Trim()))
                        {
                            var texto = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : null;
                            if (!string.IsNullOrWhiteSpace(texto)) proc = texto!.Trim();
                        }
                        else if (RxInicioCierre.IsMatch(nombre))
                        {
                            // Guardado como texto: 'inicio', 'cierre' o 'inicio+cierre'.
                            var v = (prop.Value.ValueKind == JsonValueKind.String
                                ? prop.Value.GetString() : prop.Value.ToString()) ?? "";
                            esInicio = v.Contains("inicio", StringComparison.OrdinalIgnoreCase);
                            esCierre = v.Contains("cierre", StringComparison.OrdinalIgnoreCase);
                        }
                        else if (RxObs.IsMatch(nombre))
                        {
                            var texto = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : null;
                            if (!string.IsNullOrWhiteSpace(texto)) obs = texto!.Trim();
                        }
                    }

                    // Columna calculada de peso que la fila ni siquiera trae.
                    if (textos != null)
                    {
                        foreach (var ausente in plan.CalculadasAusentes(textos.Keys))
                        {
                            var vf = plan.PesoCalculado(ausente, 0, textos);
                            if (vf > 0) peso = (peso ?? 0) + vf;
                        }
                    }

                    // ── Filas que no aportan ─────────────────────────────
                    //
                    // El operario agrega renglones de más y los deja a medias.
                    // Incluirlos llena el reporte de líneas en cero que después
                    // hay que filtrar a mano, y peor: cuentan como registros en
                    // los totales aunque no tengan nada.
                    //
                    // Se descarta la fila si no aporta NINGÚN dato de trabajo:
                    // ni personal, ni peso, ni una jornada completa.

                    var hayPersonal = planta > 0 || externo > 0;
                    var hayPeso     = peso is > 0;
                    // Una hora suelta no es una jornada: si solo se puso el
                    // inicio y se dejó el fin vacío, no hay nada que medir.
                    var hayJornada  = ini is not null && fin is not null;

                    if (!hayPersonal && !hayPeso && !hayJornada)
                        continue;

                    // Fila con horario pero sin personal ni peso: es un renglón
                    // que alguien empezó y abandonó. Se descarta salvo que traiga
                    // una observación, que sí puede ser información real
                    // ("de 10 a 11 paró la máquina").
                    if (!hayPersonal && !hayPeso && string.IsNullOrWhiteSpace(obs))
                        continue;

                    filasPersonal.Add((ini, fin, planta, externo, peso, obs, proc, esInicio, esCierre));
                }
            }

            if (!encontrado || filasPersonal.Count == 0)
                return new List<PersonalExtraidoDto>();

            // Datos que son del FORMULARIO y se repiten en todas sus filas.
            var codigo   = form.Template?.Codigo ?? "";
            var proceso  = ProcesoDelFormulario(form);
            var destino  = DestinoDelFormulario(form);
            var lote     = LoteDelFormulario(form);
            // Encabezado primero; si no la tiene (PD-05/06/07/11), de las tablas.
            var especie  = EspecieDelFormulario(form) ?? EspecieDeLasTablas(form);
            var fecha    = form.FechaRegistro ?? FechaDelFormulario(form);
            var nombreForm = form.Template?.Nombre ?? "N/A";

            var salida = new List<PersonalExtraidoDto>();

            // ── TOTAL DE HORAS DEL FORMULARIO ────────────────────────────
            //
            // Suma de todas las franjas: un registro con 07-08, 08-12 y 12-16
            // da 9 horas de jornada.
            //
            // Se escribe en TODAS las filas para que ninguna quede vacía, y
            // además en HorasTotalesUnicas solo en una: esa es la que se puede
            // sumar en Excel sin contar el mismo formulario tres veces.
            var horasDelForm = filasPersonal
                .Select(f => CalcularHoras(f.Ini, f.Fin))
                .Where(h => h.HasValue)
                .Sum(h => h!.Value);

            var idxTotal = filasPersonal.FindLastIndex(f => f.EsCierre);
            if (idxTotal < 0) idxTotal = filasPersonal.Count - 1;

            // ── TOTAL PRODUCIDO DEL FORMULARIO ───────────────────────────
            // Suma del peso de todas las franjas. Si ninguna fila lo trae, se
            // toma el de la tabla de producción: hay formatos que registran el
            // peso ahí y no por franja.
            // El peso puede estar en la tabla de personal (una celda por franja)
            // o en la de producción (una sola vez para todo el formulario).
            var pesoDeFilas = filasPersonal.Where(f => f.Peso is > 0).Sum(f => f.Peso!.Value);
            var pesoDeTabla = PesoDeProduccion(form) ?? 0;
            var pesoTotal = pesoDeFilas > 0 ? pesoDeFilas : pesoDeTabla;

            for (var idx = 0; idx < filasPersonal.Count; idx++)
            {
                var f = filasPersonal[idx];
                var llevaTotal = idx == idxTotal && horasDelForm > 0;

                salida.Add(new PersonalExtraidoDto
                {
                    FormID = form.FormID,
                    TemplateID = form.TemplateID,
                    Codigo = codigo,
                    Formulario = nombreForm,

                    // El proceso de la FILA si lo trae (el PD-15 tiene TIPO DE
                    // PROCESO por renglón); si no, el del formulario.
                    Proceso = string.IsNullOrWhiteSpace(f.Proceso) ? proceso : f.Proceso,

                    Destino = destino,
                    Lote = lote,
                    Especie = especie,
                    Fecha = fecha,

                    PersonalPlanta = f.Planta,
                    PersonalExterno = f.Externo,
                    HoraInicio = f.Ini,
                    HoraFin = f.Fin,
                    HorasTrabajadas = CalcularHoras(f.Ini, f.Fin),

                    // El peso de ESTA franja si la fila lo trae.
                    //
                    // Si no, el del formulario. Antes eso solo se hacía cuando
                    // había UNA franja, para no repartir un total entre varias e
                    // inventar un dato — pero el resultado era peor: formularios
                    // como el #2365, con 4.100 Lbs registradas en la tabla de
                    // proceso, salían con la columna vacía solo por tener tres
                    // franjas de personal.
                    //
                    // Ahora se muestra el total del formulario, igual en todas
                    // sus filas. No es el peso de esa franja y no hay forma de
                    // saberlo —el operario no lo desglosó—, pero decir "no hay
                    // peso" cuando sí lo hay es un error mayor que repetir el
                    // total. Para sumar sin duplicar está PesoTotalUnico.
                    PesoProduccion = f.Peso ?? (pesoDeTabla > 0 ? pesoDeTabla : (decimal?)null),


                    Observaciones = f.Obs,
                    EsInicio = f.EsInicio,
                    EsCierre = f.EsCierre,
                    HorasTotales = horasDelForm > 0 ? horasDelForm : null,
                    HorasTotalesUnicas = llevaTotal ? horasDelForm : null,

                    // El total producido: en todas las filas para que se vea, y
                    // una sola vez en la versión sumable.
                    PesoTotalFormulario = pesoTotal > 0 ? pesoTotal : null,
                    PesoTotalUnico = llevaTotal && pesoTotal > 0 ? pesoTotal : null,
                });
            }

            return salida;
        }

        private static int ParseEntero(JsonElement value)
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.Number:
                    return value.TryGetInt32(out int i) ? i : (int)value.GetDouble();
                case JsonValueKind.String:
                    var texto = (value.GetString() ?? "").Trim();
                    if (int.TryParse(texto, out int resultado)) return resultado;
                    // "3.0", "12 personas": se toma el primer número.
                    var m = Regex.Match(texto, @"-?\d+([.,]\d+)?");
                    return m.Success && decimal.TryParse(m.Value.Replace(",", "."),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var d)
                        ? (int)Math.Round(d) : 0;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// Número decimal de una celda, tolerando lo que se escribe en planta:
        /// "2830.00", "2.830,00", "872 Lbs". Sin quitar la unidad, un peso con
        /// "Lbs" al lado no se reconocería y la columna saldría vacía.
        /// </summary>
        private static decimal ParseDecimal(JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.Number)
                return value.TryGetDecimal(out var d) ? d : (decimal)value.GetDouble();

            if (value.ValueKind != JsonValueKind.String) return 0;

            var texto = (value.GetString() ?? "").Trim();
            if (texto.Length == 0) return 0;

            // Quitar unidades: "872 Lbs", "12kg"
            texto = Regex.Replace(texto, @"\s*(lbs?|libras?|kgs?|kilos?|oz|gr)\.?\s*$", "",
                                  RegexOptions.IgnoreCase).Trim();

            // Espacios como separador de miles: "13 017,00"
            texto = Regex.Replace(texto, @"\s+", "");

            // Separadores. Antes "14,200.00" (formato inglés) quedaba como
            // "14.200.00", no se podía leer y el peso salía VACÍO.
            //   ambos    → el último es el decimal: 2.830,00 / 2,830.00
            //   solo ,   → miles si son grupos de 3 (5,800), si no decimal (1318,20)
            //   solo .   → miles si son grupos de 3 (8.529), si no decimal (1318.20)
            var ultPunto = texto.LastIndexOf('.');
            var ultComa  = texto.LastIndexOf(',');
            if (ultPunto >= 0 && ultComa >= 0)
            {
                texto = ultComa > ultPunto
                    ? texto.Replace(".", "").Replace(",", ".")
                    : texto.Replace(",", "");
            }
            else if (ultComa >= 0)
            {
                texto = Regex.IsMatch(texto, @"^-?\d{1,3}(,\d{3})+$")
                    ? texto.Replace(",", "")
                    : texto.Replace(",", ".");
            }
            else if (ultPunto >= 0 && Regex.IsMatch(texto, @"^-?\d{1,3}(\.\d{3})+$"))
            {
                texto = texto.Replace(".", "");
            }

            return decimal.TryParse(texto, System.Globalization.NumberStyles.Float,
                                    System.Globalization.CultureInfo.InvariantCulture, out var r) ? r : 0;
        }

        private static TimeSpan? ParseHora(JsonElement value)
        {
            string? texto = value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetDouble().ToString(),
                _ => null
            };

            if (string.IsNullOrWhiteSpace(texto))
                return null;
            if (TimeSpan.TryParse(texto, out var ts))
                return ts;
            if (DateTime.TryParse(texto, out var dt))
                return dt.TimeOfDay;

            return null;
        }

    }
}