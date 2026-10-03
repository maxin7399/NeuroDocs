namespace NeuroDocs.Models;

/// <summary>Campos de identificación del paciente y sus posibles etiquetas en los documentos.</summary>
public static class CamposPaciente
{
    public const string Nombre = "Nombre";
    public const string Identificacion = "Número de identificación";
    public const string LugarFechaNacimiento = "Lugar y fecha de nacimiento";
    public const string Edad = "Edad";
    public const string Genero = "Género";
    public const string Escolaridad = "Escolaridad";
    public const string Lateralidad = "Lateralidad";
    public const string ProblemasColegio = "Problemas en el colegio";
    public const string Ocupacion = "Ocupación";
    public const string EstadoCivil = "Estado civil";
    public const string Direccion = "Dirección";
    public const string Telefonos = "Números telefónicos";
    public const string RemitidoPor = "Remitido por";
    public const string Entidad = "Entidad";
    public const string TipoVinculacion = "Tipo de Vinculación";
    public const string FechaEntrevista = "Fecha de la entrevista";
    public const string Acompanante = "Acompañante";
    public const string Email = "Email";

    /// <summary>En el orden en que aparecen en la plantilla.</summary>
    public static readonly IReadOnlyList<string> Todos =
    [
        Nombre, Identificacion, LugarFechaNacimiento, Edad, Genero, Escolaridad,
        Lateralidad, ProblemasColegio,
        Ocupacion, EstadoCivil, Direccion, Telefonos, RemitidoPor, Entidad,
        TipoVinculacion, FechaEntrevista, Acompanante, Email
    ];

    /// <summary>Si no aparecen en el ECC, su línea se elimina del plan.</summary>
    public static readonly IReadOnlySet<string> Opcionales =
        new HashSet<string> { Lateralidad, ProblemasColegio };

    /// <summary>Campo → etiquetas con las que puede aparecer (en el ECC o en la plantilla).</summary>
    public static readonly IReadOnlyDictionary<string, string[]> Etiquetas = new Dictionary<string, string[]>
    {
        [Nombre] = ["Nombre"],
        [Identificacion] = ["Número de identificación"],
        [LugarFechaNacimiento] = ["Lugar y fecha de nacimiento"],
        [Edad] = ["Edad"],
        [Genero] = ["Género"],
        [Escolaridad] = ["Escolaridad"],
        [Lateralidad] = ["Lateralidad"],
        [ProblemasColegio] = ["Problemas en el colegio"],
        [Ocupacion] = ["Ocupación"],
        [EstadoCivil] = ["Estado civil"],
        [Direccion] = ["Dirección Completa", "Dirección"],
        [Telefonos] = ["Números telefónicos"],
        [RemitidoPor] = ["Remitido por"],
        [Entidad] = ["Entidad"],
        [TipoVinculacion] = ["Tipo de Vinculación"],
        [FechaEntrevista] = ["Fecha de la entrevista"],
        [Acompanante] = ["Acompañante"],
        [Email] = ["Email", "Correo"],
    };
}