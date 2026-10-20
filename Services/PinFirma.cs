#nullable disable
// Sintaxis conservadora para poder probarlo también fuera de .NET 9.
using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace FormBuilder.API.Services
{
    /// <summary>
    /// 🔑 PIN de firma: hash y límite de intentos.
    ///
    /// Un PIN de 4 dígitos tiene solo 10.000 combinaciones: sin límite se adivina
    /// en minutos. Tras MaxFallos PIN incorrectos seguidos para una misma
    /// persona, esa persona queda bloqueada por Bloqueo. Un PIN correcto
    /// reinicia el contador. El conteo vive en memoria del servidor (se
    /// reinicia al reiniciar la API), suficiente para cortar un ataque.
    /// </summary>
    public static class PinFirma
    {
        public const int MaxFallos = 5;
        public static readonly TimeSpan Bloqueo = TimeSpan.FromMinutes(15);

        private sealed class Estado
        {
            public int Fallos;
            public DateTime? BloqueadoHasta;
        }

        private static readonly ConcurrentDictionary<string, Estado> Intentos =
            new ConcurrentDictionary<string, Estado>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Hash SHA-256 (hex en mayúsculas), igual que CatalogoFirmasController.</summary>
        public static string Hash(string pin)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes((pin ?? "").Trim()));
                var sb = new StringBuilder(bytes.Length * 2);
                foreach (var b in bytes) sb.Append(b.ToString("X2"));
                return sb.ToString();
            }
        }

        private static string Clave(string persona)
        {
            return (persona ?? "").Trim().ToLowerInvariant();
        }

        /// <summary>Tiempo que falta de bloqueo, o null si puede intentar.</summary>
        public static TimeSpan? Bloqueado(string persona, DateTime? ahora = null)
        {
            Estado e;
            if (!Intentos.TryGetValue(Clave(persona), out e)) return null;
            var t = ahora ?? DateTime.Now;
            lock (e)
            {
                if (e.BloqueadoHasta.HasValue && e.BloqueadoHasta.Value > t) return e.BloqueadoHasta.Value - t;
                if (e.BloqueadoHasta.HasValue) { e.BloqueadoHasta = null; e.Fallos = 0; }   // ya pasó
                return null;
            }
        }

        /// <summary>Registra un PIN incorrecto. Devuelve los intentos que quedan (0 = bloqueado).</summary>
        public static int Fallo(string persona, DateTime? ahora = null)
        {
            var e = Intentos.GetOrAdd(Clave(persona), _ => new Estado());
            var t = ahora ?? DateTime.Now;
            lock (e)
            {
                e.Fallos++;
                if (e.Fallos >= MaxFallos)
                {
                    e.BloqueadoHasta = t + Bloqueo;
                    return 0;
                }
                return MaxFallos - e.Fallos;
            }
        }

        /// <summary>PIN correcto: se olvidan los fallos.</summary>
        public static void Exito(string persona)
        {
            Estado e;
            Intentos.TryRemove(Clave(persona), out e);
        }

        /// <summary>Solo para pruebas.</summary>
        public static void Reiniciar()
        {
            Intentos.Clear();
        }
    }
}
