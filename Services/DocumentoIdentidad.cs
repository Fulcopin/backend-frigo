#nullable disable
// Sintaxis conservadora para poder probarlo también fuera de .NET 9.
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace FormBuilder.API.Services
{
    /// <summary>
    /// 🪪 Validación de documentos de identidad para firmar sin usuario.
    ///
    /// Cédula ecuatoriana: 10 dígitos, provincia 01-24 (o 30 para registros
    /// en el exterior), tercer dígito menor que 6 y dígito verificador
    /// (módulo 10). Detecta los errores de tipeo más comunes.
    /// Pasaporte: letras y números, de 5 a 20 caracteres.
    /// </summary>
    public static class DocumentoIdentidad
    {
        public const string Cedula = "cedula";
        public const string Pasaporte = "pasaporte";

        private static readonly Regex RxPasaporte = new Regex(@"^[A-Z0-9]{5,20}$", RegexOptions.Compiled);

        /// <summary>Quita espacios, guiones y puntos; pasa a mayúsculas.</summary>
        public static string Limpiar(string numero)
        {
            return Regex.Replace((numero ?? "").ToUpperInvariant(), @"[\s\-\.]", "");
        }

        public static bool CedulaValida(string numero)
        {
            var c = Limpiar(numero);
            if (c.Length != 10 || !c.All(char.IsDigit)) return false;

            var provincia = int.Parse(c.Substring(0, 2));
            if (!((provincia >= 1 && provincia <= 24) || provincia == 30)) return false;
            if (c[2] - '0' >= 6) return false;

            var suma = 0;
            for (var i = 0; i < 9; i++)
            {
                var v = (c[i] - '0') * (i % 2 == 0 ? 2 : 1);
                suma += v > 9 ? v - 9 : v;
            }
            var verificador = (10 - suma % 10) % 10;
            return verificador == c[9] - '0';
        }

        public static bool PasaporteValido(string numero)
        {
            return RxPasaporte.IsMatch(Limpiar(numero));
        }

        /// <summary>Mensaje de error, o null si el documento es válido.</summary>
        public static string Validar(string tipo, string numero)
        {
            var t = (tipo ?? Cedula).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(numero)) return "Ingresa el número de documento.";
            if (t == Pasaporte)
                return PasaporteValido(numero) ? null : "El pasaporte debe tener de 5 a 20 letras o números.";
            if (t != Cedula) return "Tipo de documento no válido.";
            if (!CedulaValida(numero))
                return "El número de cédula no es válido. Revisa que tenga 10 dígitos y esté bien escrito.";
            return null;
        }

        /// <summary>Etiqueta corta para el texto de la celda.</summary>
        public static string Etiqueta(string tipo)
        {
            return (tipo ?? "").Trim().ToLowerInvariant() == Pasaporte ? "Pasaporte" : "C.I.";
        }
    }
}
