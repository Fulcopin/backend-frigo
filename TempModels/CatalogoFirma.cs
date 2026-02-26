using System;
using System.Collections.Generic;

namespace FormBuilder.API.TempModels;

public partial class CatalogoFirma
{
    public int CatalogoFirmaId { get; set; }

    public string Puesto { get; set; } = null!;

    public string? NombreCompleto { get; set; }

    public string? Area { get; set; }

    public bool Activo { get; set; }

    public DateTime FechaCreacion { get; set; }

    public string? Correo { get; set; }
}
