#nullable disable
// Escrito con sintaxis conservadora (sin anotaciones de nulos) para poder
// probarlo también fuera de .NET 9.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace FormBuilder.API.Services
{
    /// <summary>
    /// 🧮 Evalúa en el SERVIDOR las fórmulas de columnas de la plantilla
    /// (ej. TOTAL Lbs NETAS = [PESO PT] > 0 ? [PESO PT] : [TOTAL CAJAS] * [CAPACIDAD]).
    ///
    /// Por qué: la pantalla calcula las fórmulas al dibujar, pero en la base
    /// muchos registros quedaron con "0.00" en la columna calculada. Los reportes
    /// que se arman en el backend (Personal) no tenían cómo recalcularla y
    /// mostraban 0 aunque el operario hubiera cargado cajas y capacidad.
    ///
    /// Sigue las reglas de src/utils/formulaEngine.js para dar el mismo número:
    ///   - cada nombre de columna se reemplaza por su valor (el más largo
    ///     primero, sin distinguir mayúsculas ni tildes); vacío = 0
    ///   - [ ] se toman como paréntesis; "5x20" es multiplicación
    ///   - + - * / ( ), comparaciones, && || ! y condicional ? :
    ///   - porcentaje(expr) multiplica por 100; sum(A, B) suma
    /// Lo que no se puede resolver con los datos de UNA fila (sumas de columna
    /// [*], filas [N], _ROW_, nombres que no existen) devuelve null: mejor no
    /// mostrar nada que inventar un número.
    /// </summary>
    public static class EvaluadorFormulas
    {
        private static readonly Regex RxPorcentaje = new Regex(
            @"^(?:porcentaje|percent|pct)\((.+)\)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex RxOtraFila = new Regex(
            @"\[\s*(\*|\d+|max|min|count)\s*\]|_row_", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex RxPermitidos = new Regex(
            @"^[0-9.+\-*/()?:<>=&|!]+$", RegexOptions.Compiled);
        private static readonly Regex RxPor = new Regex(
            @"([0-9)])x([0-9(])", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>Texto comparable: sin tildes, minúsculas, espacios simples.</summary>
        public static string Normalizar(string texto)
        {
            if (string.IsNullOrEmpty(texto)) return string.Empty;
            var descompuesto = texto.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(descompuesto.Length);
            foreach (var c in descompuesto)
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            return Regex.Replace(sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant(), @"\s+", " ").Trim();
        }

        /// <summary>
        /// Nombres de columna que aparecen dentro de la fórmula (las entradas).
        /// </summary>
        public static HashSet<string> Referencias(string formula, IEnumerable<string> columnas)
        {
            var usadas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(formula)) return usadas;
            var f = Normalizar(formula);
            foreach (var col in columnas.Where(c => !string.IsNullOrWhiteSpace(c))
                                        .OrderByDescending(c => c.Length))
            {
                var n = Normalizar(col);
                if (n.Length == 0 || f.IndexOf(n, StringComparison.Ordinal) < 0) continue;
                usadas.Add(col);
                f = f.Replace(n, " ");   // "PESO PT" no debe contar además como "PESO"
            }
            return usadas;
        }

        /// <summary>
        /// Valor de la fórmula para una fila, o null si no se puede calcular.
        /// </summary>
        /// <param name="valores">nombre de columna → texto guardado en la celda</param>
        public static decimal? Evaluar(string formula, IDictionary<string, string> valores)
        {
            if (string.IsNullOrWhiteSpace(formula) || valores == null) return null;
            var f = formula.Trim();

            var pct = RxPorcentaje.Match(f);
            if (pct.Success)
            {
                var interno = Evaluar(pct.Groups[1].Value, valores);
                return interno.HasValue ? Math.Round(interno.Value * 100m, 6) : (decimal?)null;   // el interno viene con 10 decimales
            }

            if (f.StartsWith("sum(", StringComparison.OrdinalIgnoreCase) && f.EndsWith(")"))
            {
                decimal suma = 0;
                foreach (var nombre in f.Substring(4, f.Length - 5).Split(','))
                {
                    string texto;
                    if (Buscar(valores, nombre.Trim(), out texto)) suma += Numero(texto);
                }
                return suma;
            }

            if (RxOtraFila.IsMatch(f)) return null;

            // Reemplazo de nombres por valores, el más largo primero.
            var expr = Normalizar(f);
            var porNombre = new Dictionary<string, decimal>(StringComparer.Ordinal);
            foreach (var par in valores)
            {
                var n = Normalizar(par.Key);
                if (n.Length > 0 && !porNombre.ContainsKey(n)) porNombre[n] = Numero(par.Value);
            }
            foreach (var n in porNombre.Keys.OrderByDescending(k => k.Length))
            {
                if (expr.IndexOf(n, StringComparison.Ordinal) < 0) continue;
                expr = expr.Replace(n, "(" + porNombre[n].ToString(CultureInfo.InvariantCulture) + ")");
            }

            // Mismo saneo que el motor del frontend.
            expr = Regex.Replace(expr, @"\s", "")
                .Replace('[', '(').Replace(']', ')');
            expr = RxPor.Replace(expr, "$1*$2");
            expr = expr.Replace("++", "+").Replace("--", "+")
                       .Replace("+-", "-").Replace("-+", "-")
                       .Replace("*+", "*").Replace("/+", "/");

            if (expr.Length == 0 || !RxPermitidos.IsMatch(expr)) return null;   // quedó un nombre sin resolver

            try
            {
                var parser = new Parser(expr);
                var resultado = parser.Expresion();
                if (!parser.Terminado) return null;
                if (double.IsNaN(resultado) || double.IsInfinity(resultado)) return 0m;
                // 10 decimales: porcentaje() multiplica por 100 después y no
                // debe perder precisión. El reporte muestra 2.
                return Math.Round((decimal)resultado, 10);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool Buscar(IDictionary<string, string> valores, string nombre, out string texto)
        {
            var n = Normalizar(nombre.Trim('[', ']', ' '));
            foreach (var par in valores)
            {
                if (Normalizar(par.Key) == n) { texto = par.Value; return true; }
            }
            texto = null;
            return false;
        }

        /// <summary>
        /// Texto de una celda → número. Vacío o ilegible = 0 (como el motor del
        /// frontend). Acepta "13.017,00", "13,017.00", "5,800", "1318,20", "872 Lbs".
        /// </summary>
        public static decimal Numero(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return 0m;
            var t = Regex.Replace(texto.Trim(), @"\s*(lbs?|libras?|kgs?|kilos?|oz|gr|%|°c)\.?\s*$", "", RegexOptions.IgnoreCase);
            t = Regex.Replace(t, @"\s+", "");

            var punto = t.LastIndexOf('.');
            var coma = t.LastIndexOf(',');
            if (punto >= 0 && coma >= 0)
                t = coma > punto ? t.Replace(".", "").Replace(",", ".") : t.Replace(",", "");
            else if (coma >= 0)
                t = Regex.IsMatch(t, @"^-?\d{1,3}(,\d{3})+$") ? t.Replace(",", "") : t.Replace(",", ".");
            else if (punto >= 0 && Regex.IsMatch(t, @"^-?\d{1,3}(\.\d{3})+$"))
                t = t.Replace(".", "");

            decimal r;
            return decimal.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out r) ? r : 0m;
        }

        // ── Evaluador de expresiones (sin eval, sin reflexión) ────────────
        // Precedencia, de menor a mayor:
        //   ?:   ||   &&   == != === !==   < > <= >=   + -   * /   ! - + (unarios)
        private sealed class Parser
        {
            private readonly string _s;
            private int _i;

            public Parser(string s) { _s = s; }

            public bool Terminado { get { return _i >= _s.Length; } }

            private bool Viene(string op)
            {
                if (string.CompareOrdinal(_s, _i, op, 0, op.Length) != 0) return false;
                _i += op.Length;
                return true;
            }

            private static bool Verdadero(double v) { return v != 0 && !double.IsNaN(v); }

            public double Expresion()
            {
                var condicion = O();
                if (Viene("?"))
                {
                    var si = Expresion();
                    if (!Viene(":")) throw new FormatException("falta ':'");
                    var no = Expresion();
                    return Verdadero(condicion) ? si : no;
                }
                return condicion;
            }

            private double O()
            {
                var a = Y();
                while (Viene("||")) { var b = Y(); a = Verdadero(a) ? a : b; }
                return a;
            }

            private double Y()
            {
                var a = Igualdad();
                while (Viene("&&")) { var b = Igualdad(); a = Verdadero(a) ? b : a; }
                return a;
            }

            private double Igualdad()
            {
                var a = Relacion();
                while (true)
                {
                    if (Viene("===") || Viene("==")) a = a == Relacion() ? 1 : 0;
                    else if (Viene("!==") || Viene("!=")) a = a != Relacion() ? 1 : 0;
                    else return a;
                }
            }

            private double Relacion()
            {
                var a = Suma();
                while (true)
                {
                    if (Viene("<=")) a = a <= Suma() ? 1 : 0;
                    else if (Viene(">=")) a = a >= Suma() ? 1 : 0;
                    else if (Viene("<")) a = a < Suma() ? 1 : 0;
                    else if (Viene(">")) a = a > Suma() ? 1 : 0;
                    else return a;
                }
            }

            private double Suma()
            {
                var a = Producto();
                while (true)
                {
                    if (Viene("+")) a += Producto();
                    else if (Viene("-")) a -= Producto();
                    else return a;
                }
            }

            private double Producto()
            {
                var a = Unario();
                while (true)
                {
                    if (Viene("*")) a *= Unario();
                    else if (Viene("/")) a /= Unario();
                    else return a;
                }
            }

            private double Unario()
            {
                if (Viene("!")) return Verdadero(Unario()) ? 0 : 1;
                if (Viene("-")) return -Unario();
                if (Viene("+")) return Unario();
                return Primario();
            }

            private double Primario()
            {
                if (Viene("("))
                {
                    var v = Expresion();
                    if (!Viene(")")) throw new FormatException("falta ')'");
                    return v;
                }

                var inicio = _i;
                while (_i < _s.Length && (char.IsDigit(_s[_i]) || _s[_i] == '.')) _i++;
                if (_i == inicio) throw new FormatException("se esperaba un número");
                return double.Parse(_s.Substring(inicio, _i - inicio), NumberStyles.Float, CultureInfo.InvariantCulture);
            }
        }
    }

    /// <summary>Columna de una tabla, tal como la define la plantilla.</summary>
    public sealed class ColumnaPlantilla
    {
        public string Label { get; set; }
        public string Tipo { get; set; }
        public string Formula { get; set; }
        public string Rol { get; set; }
    }

    /// <summary>
    /// Cómo sacar el PESO de las filas de una tabla que tiene una columna
    /// calculada de peso (ej. LIBRAS NETAS = PESO PT o CAJAS × CAPACIDAD).
    ///
    ///  - La columna calculada se lee de la fila; si quedó en 0 o vacía, se
    ///    calcula con la fórmula de la plantilla.
    ///  - Las columnas de peso que ALIMENTAN esa fórmula (ej. PESO PT) no se
    ///    suman aparte: ya están dentro del resultado. Sumarlas contaba el
    ///    mismo peso dos veces.
    /// Sin columnas calculadas de peso, no cambia nada.
    /// </summary>
    public sealed class CalculoPesoTabla
    {
        private static readonly Regex RxSufijo = new Regex(@"_col\d+$", RegexOptions.Compiled);

        private readonly Dictionary<string, string> _formulas =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _entradas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _columnasDato = new List<string>();

        public static readonly CalculoPesoTabla Ninguno = new CalculoPesoTabla();

        public bool TieneFormulas { get { return _formulas.Count > 0; } }
        public IEnumerable<string> ColumnasCalculadas { get { return _formulas.Keys; } }

        /// <param name="columnas">columnas de la tabla en la plantilla</param>
        /// <param name="esColumnaPeso">(etiqueta, rol) → ¿cuenta como peso producido?</param>
        public static CalculoPesoTabla Crear(IEnumerable<ColumnaPlantilla> columnas, Func<string, string, bool> esColumnaPeso)
        {
            var plan = new CalculoPesoTabla();
            var lista = (columnas ?? Enumerable.Empty<ColumnaPlantilla>())
                .Where(c => c != null && !string.IsNullOrWhiteSpace(c.Label)).ToList();

            foreach (var c in lista)
            {
                var tipo = (c.Tipo ?? "").Trim().ToLowerInvariant();
                var esCalculada = (tipo == "formula" || tipo == "calculated") && !string.IsNullOrWhiteSpace(c.Formula);
                if (esCalculada && esColumnaPeso(c.Label.Trim(), c.Rol))
                    plan._formulas[c.Label.Trim()] = c.Formula;
                else if (!esCalculada)
                    plan._columnasDato.Add(c.Label.Trim());
            }

            if (plan._formulas.Count == 0) return plan;

            var nombres = lista.Select(c => c.Label.Trim()).ToList();
            foreach (var formula in plan._formulas)
            {
                foreach (var usada in EvaluadorFormulas.Referencias(formula.Value, nombres))
                {
                    if (plan._formulas.ContainsKey(usada)) continue;
                    var col = lista.First(c => string.Equals(c.Label.Trim(), usada, StringComparison.OrdinalIgnoreCase));
                    if (esColumnaPeso(usada, col.Rol)) plan._entradas.Add(usada);
                }
            }
            return plan;
        }

        public static string SinSufijo(string clave)
        {
            return RxSufijo.Replace(clave ?? "", "").Trim();
        }

        /// <summary>¿Es una columna de peso que ya está dentro de una calculada?</summary>
        public bool EsEntrada(string clave)
        {
            return _entradas.Contains(SinSufijo(clave));
        }

        /// <summary>Fórmula si la columna es la calculada de peso; si no, null.</summary>
        public string FormulaDe(string clave)
        {
            string f;
            return _formulas.TryGetValue(SinSufijo(clave), out f) ? f : null;
        }

        /// <summary>
        /// Peso de la columna calculada en esta fila: el guardado si es mayor
        /// que 0; si no, el que da la fórmula.
        /// </summary>
        public decimal PesoCalculado(string clave, decimal guardado, IDictionary<string, string> fila)
        {
            if (guardado > 0) return guardado;
            var formula = FormulaDe(clave);
            if (formula == null) return guardado;

            // Claves "ETIQUETA_colN" (columnas repetidas) también valen por su etiqueta.
            var valores = new Dictionary<string, string>(fila, StringComparer.OrdinalIgnoreCase);
            foreach (var par in fila)
            {
                var limpia = SinSufijo(par.Key);
                if (limpia.Length > 0 && !valores.ContainsKey(limpia)) valores[limpia] = par.Value;
            }
            // Las columnas de la plantilla que la fila no trae cuentan como vacías.
            foreach (var col in _columnasDato)
                if (!valores.ContainsKey(col)) valores[col] = "";

            var r = EvaluadorFormulas.Evaluar(formula, valores);
            return r.HasValue && r.Value > 0 ? r.Value : 0m;
        }

        /// <summary>Columnas calculadas que la fila no trae (hay que calcularlas igual).</summary>
        public IEnumerable<string> CalculadasAusentes(IEnumerable<string> clavesDeLaFila)
        {
            var presentes = new HashSet<string>(clavesDeLaFila.Select(SinSufijo), StringComparer.OrdinalIgnoreCase);
            return _formulas.Keys.Where(k => !presentes.Contains(k)).ToList();
        }
    }
}
