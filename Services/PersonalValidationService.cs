using FormBuilder.API.Models;

namespace FormBuilder.API.Services
{
    public class ValidacionPersonalResultado
    {
        public bool CumpleEstandar { get; set; } = true;
        public string? Motivo { get; set; }
        public bool RequiereJustificacion { get; set; }
    }

    /// <summary>
    /// Valida restricciones operativas de tiempo para registros de Personal.
    /// Reglas determinísticas (sin IA): compara horario/duración registrados
    /// contra el estándar configurado para el proceso, con tolerancia en minutos.
    /// </summary>
    public class PersonalValidationService
    {
        public ValidacionPersonalResultado Validar(
            TimeSpan horaDesde,
            TimeSpan horaHasta,
            ProcesoEstandar? estandar)
        {
            var resultado = new ValidacionPersonalResultado();

            if (estandar is null || !estandar.Activo)
                return resultado; // sin estándar definido para el proceso, no se exige nada

            var duracion = (decimal)(horaHasta - horaDesde).TotalHours;
            var tolerancia = TimeSpan.FromMinutes(estandar.ToleranciaMinutos);

            var motivos = new List<string>();

            if (estandar.HoraInicioEsperada is TimeSpan inicioEsperado &&
                horaDesde > inicioEsperado + tolerancia)
                motivos.Add("inicio_tardio");

            if (estandar.HoraFinEsperada is TimeSpan finEsperado &&
                horaHasta < finEsperado - tolerancia)
                motivos.Add("fin_temprano");

            if (estandar.DuracionMinimaHoras is decimal minH && duracion < minH)
                motivos.Add("duracion_menor_al_minimo");

            if (estandar.DuracionMaximaHoras is decimal maxH && duracion > maxH)
                motivos.Add("duracion_mayor_al_maximo");

            if (motivos.Count > 0)
            {
                resultado.CumpleEstandar = false;
                resultado.RequiereJustificacion = true;
                resultado.Motivo = string.Join(",", motivos);
            }

            return resultado;
        }
    }
}
