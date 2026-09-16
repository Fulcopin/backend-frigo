using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FormBuilder.API.Models
{
    /// <summary>
    /// Estándar operativo de tiempo por proceso (ej: mínimo 8 horas, turno 06:00-14:00).
    /// Alimenta la validación automática de PersonalRegistro sin necesidad de IA:
    /// es una simple comparación de rangos con tolerancia configurable.
    /// </summary>
    public class ProcesoEstandar
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Formulario (Template) específico al que aplica este estándar. Si se define,
        /// tiene prioridad sobre el estándar por Proceso (texto libre) al validar.
        /// </summary>
        public int? TemplateID { get; set; }

        [ForeignKey("TemplateID")]
        public virtual Template? Template { get; set; }

        /// <summary>Copia del nombre del formulario al momento de configurar, solo para mostrar en listados.</summary>
        [StringLength(255)]
        public string? FormularioNombre { get; set; }

        /// <summary>Debe coincidir con Template.Proceso / PersonalRegistro.Proceso. Se usa como respaldo cuando TemplateID es null.</summary>
        [Required]
        [StringLength(200)]
        public string Proceso { get; set; } = string.Empty;

        public TimeSpan? HoraInicioEsperada { get; set; }
        public TimeSpan? HoraFinEsperada { get; set; }

        [Column(TypeName = "decimal(6,2)")]
        public decimal? DuracionMinimaHoras { get; set; }

        [Column(TypeName = "decimal(6,2)")]
        public decimal? DuracionMaximaHoras { get; set; }

        /// <summary>Tolerancia en minutos antes de marcar incumplimiento.</summary>
        public int ToleranciaMinutos { get; set; } = 0;

        public bool Activo { get; set; } = true;

        public DateTime CreadoEn { get; set; } = DateTime.Now;
        public DateTime? ActualizadoEn { get; set; }
    }

    public class ProcesoEstandarCreateDto
    {
        public int? TemplateID { get; set; }

        [Required]
        public string Proceso { get; set; } = string.Empty;
        public TimeSpan? HoraInicioEsperada { get; set; }
        public TimeSpan? HoraFinEsperada { get; set; }
        public decimal? DuracionMinimaHoras { get; set; }
        public decimal? DuracionMaximaHoras { get; set; }
        public int ToleranciaMinutos { get; set; } = 0;
        public bool Activo { get; set; } = true;
    }

    /// <summary>
    /// Registro de "Control del Personal" extraído directamente de la tabla dinámica
    /// dentro de un FilledForm (columnas tipo HORA INICIO/HORA FIN/PERSONAL PLANTA/
    /// PERSONAL EXTERNO/OBSERVACIÓN), sumando filas y calculando el rango horario
    /// real del formulario, sin requerir doble digitación manual.
    /// </summary>
    public class PersonalExtraidoDto
    {
        public int FormID { get; set; }
        public int TemplateID { get; set; }

        /// <summary>Código del formato: FOR-PD-04, FOR-PD-14. Es como se lo
        /// conoce en planta, más corto y sin ambigüedad que el nombre largo.</summary>
        public string Codigo { get; set; } = string.Empty;

        public string Formulario { get; set; } = string.Empty;
        public string Proceso { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public int PersonalPlanta { get; set; }
        public int PersonalExterno { get; set; }
        public TimeSpan? HoraInicio { get; set; }
        public TimeSpan? HoraFin { get; set; }
        public decimal? HorasTrabajadas { get; set; }
        public bool CumpleEstandar { get; set; } = true;

        /// <summary>
        /// A qué empresa va el proceso: Frigolab, San Mateo, Ecuatun. Sale del
        /// encabezado y sirve para separar la planificación por destino.
        /// </summary>
        public string? Destino { get; set; }

        /// <summary>
        /// Lote que se está procesando. Es lo que identifica el FLUJO: el mismo
        /// lote pasa por recepción, fileteo y empaque, sin importar en qué orden
        /// se cargaron los formularios ni si hubo pausas entre los pasos.
        /// </summary>
        public string? Lote { get; set; }

        /// <summary>
        /// Especie que se está procesando: Swordfish, Tuna, Mahi Mahi.
        /// Sale del encabezado. Sirve para separar la producción por especie,
        /// que es lo que cambia el costo de la libra.
        /// </summary>
        public string? Especie { get; set; }

        /// <summary>
        /// Libras producidas en este formulario, tomadas de la tabla de
        /// producción. Queda en null si el formulario no declara peso: es mejor
        /// dejarlo vacío que mostrar un cero que parezca un dato real.
        /// </summary>
        public decimal? PesoProduccion { get; set; }


        /// <summary>
        /// La fila marcada como INICIO del flujo de trabajo.
        /// Lo marca el operario; no se deduce de las horas, porque en planta no
        /// hay turnos fijos y el sistema no puede saber si un tramo abre o no.
        /// </summary>
        public bool EsInicio { get; set; }

        /// <summary>La fila marcada como CIERRE: hasta acá llegó el flujo.</summary>
        public bool EsCierre { get; set; }

        /// <summary>
        /// Suma de las horas de TODAS las franjas de este formulario.
        ///
        /// Va en TODAS las filas del formulario, para que ninguna quede vacía y
        /// se pueda leer el total sin buscar la fila de salida.
        /// </summary>
        public decimal? HorasTotales { get; set; }

        /// <summary>
        /// El mismo total, pero SOLO en una fila por formulario.
        ///
        /// Existe porque HorasTotales se repite: sumar esa columna en Excel
        /// daría 27 horas donde hubo 9. Esta es la que se puede totalizar sin
        /// contar el mismo formulario tres veces.
        /// </summary>
        public decimal? HorasTotalesUnicas { get; set; }

        /// <summary>
        /// Peso TOTAL producido en el formulario, repetido en todas sus filas
        /// para que ninguna quede vacía.
        ///
        /// ⚠️ No sumar esta columna: daría el total multiplicado por la
        /// cantidad de franjas. Para totalizar está PesoTotalUnico.
        /// </summary>
        public decimal? PesoTotalFormulario { get; set; }

        /// <summary>El mismo total, una sola vez por formulario. Esta sí se suma.</summary>
        public decimal? PesoTotalUnico { get; set; }

        public string? Observaciones { get; set; }
    }
}