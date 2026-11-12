
using System.Globalization;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace PocDotnetParseDate.App;

internal static class Program
{
    private const string XmlInput = "<doc><dataNascita>1942-02-08+02:00</dataNascita></doc>";

    public static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("================================================================================");
        Console.WriteLine("   POC .NET: Parsing Date XML con Offset (+02:00) e Analisi Day Shift          ");
        Console.WriteLine("================================================================================");
        Console.WriteLine($"XML in input: {XmlInput}");
        Console.WriteLine($"Fuso orario locale macchina: {TimeZoneInfo.Local.DisplayName} (Offset attuale: {TimeZoneInfo.Local.GetUtcOffset(DateTime.Now)})\n");

        // -----------------------------------------------------------------------------
        // PARTE 1: Deserializzazione con System.Xml.Serialization.XmlSerializer
        // -----------------------------------------------------------------------------
        Console.WriteLine("=== 1. Deserializzazione XML tramite XmlSerializer ===");

        // Approccio Standard A: Mapping verso System.DateTime
        var serializerA = new XmlSerializer(typeof(DocDateTime));
        using (var readerA = new StringReader(XmlInput))
        {
            if (serializerA.Deserialize(readerA) is DocDateTime docA)
            {
                PrintResult(
                    label: "Approccio Standard A (System.DateTime)",
                    formattedDate: docA.DataNascita.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
                    day: docA.DataNascita.Day,
                    extraDetails: $"DateTimeKind = {docA.DataNascita.Kind} (convertito automaticamente all'ora locale)");
            }
        }

        // Approccio Consigliato B: Mapping verso System.DateTimeOffset
        var serializerB = new XmlSerializer(typeof(DocDateTimeOffset));
        using (var readerB = new StringReader(XmlInput))
        {
            if (serializerB.Deserialize(readerB) is DocDateTimeOffset docB)
            {
                PrintResult(
                    label: "Approccio Consigliato B (System.DateTimeOffset)",
                    formattedDate: docB.DataNascita.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
                    day: docB.DataNascita.Day,
                    extraDetails: $"Offset = {docB.DataNascita.Offset} (preserva l'offset originale senza shift locale)");
            }
        }

        // -----------------------------------------------------------------------------
        // PARTE 2: Parsing alternativo con System.Xml.Linq.XDocument
        // -----------------------------------------------------------------------------
        Console.WriteLine("=== 2. Parsing alternativo tramite XDocument & DateTimeStyles ===");
        var xDoc = XDocument.Parse(XmlInput);
        var rawValue = xDoc.Root?.Element("dataNascita")?.Value ?? string.Empty;

        // Parsing diretto su DateTime (standard)
        var parsedDt = DateTime.Parse(rawValue, CultureInfo.InvariantCulture);
        PrintResult(
            label: "XDocument -> DateTime.Parse",
            formattedDate: parsedDt.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
            day: parsedDt.Day,
            extraDetails: $"DateTimeKind = {parsedDt.Kind}");

        // Parsing con DateTimeStyles.RoundtripKind
        var parsedDtRoundtrip = DateTime.Parse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        PrintResult(
            label: "XDocument -> DateTime.Parse con DateTimeStyles.RoundtripKind",
            formattedDate: parsedDtRoundtrip.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
            day: parsedDtRoundtrip.Day,
            extraDetails: $"DateTimeKind = {parsedDtRoundtrip.Kind} (nota: se convertito a DateTime con offset non UTC, viene convertito in Local)");

        // Parsing verso DateTimeOffset (consigliato per preservare il dato esatto)
        var parsedDto = DateTimeOffset.Parse(rawValue, CultureInfo.InvariantCulture);
        PrintResult(
            label: "XDocument -> DateTimeOffset.Parse",
            formattedDate: parsedDto.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
            day: parsedDto.Day,
            extraDetails: $"Offset = {parsedDto.Offset} (raccomandato per date di nascita e dati temporali con offset)");

        Console.WriteLine("================================================================================");
        Console.WriteLine("Spiegazione tecnica:");
        Console.WriteLine("- L'input '1942-02-08+02:00' rappresenta la mezzanotte a UTC+2.");
        Console.WriteLine("- In UTC corrisponde al '1942-02-07 22:00:00Z'.");
        Console.WriteLine("- Convertito nel fuso orario locale (es. Roma UTC+1), diventa '1942-02-07 23:00:00+01:00'.");
        Console.WriteLine("- Di conseguenza, accedendo alla proprietà .Day su un System.DateTime locale, si ottiene 7 anziché 8.");
        Console.WriteLine("- Usando System.DateTimeOffset, l'offset originale (+02:00) viene preservato e .Day restituisce correttamente 8.");
        Console.WriteLine("================================================================================");
    }

    private static void PrintResult(string label, string formattedDate, int day, string extraDetails)
    {
        Console.WriteLine($"--- {label} ---");
        Console.WriteLine($"  Data/Ora formattata : {formattedDate}");
        Console.WriteLine($"  Giorno (.Day)       : {day}");
        Console.WriteLine($"  Dettagli            : {extraDetails}");

        Console.Write("  Esito               : ");
        var originalColor = Console.ForegroundColor;
        if (day == 8)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("OK: Il giorno è corretto (8)");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"ATTENZIONE: Il valore del giorno risulta scalato a {day} anziché 8!");
        }
        Console.ForegroundColor = originalColor;
        Console.WriteLine();
    }
}

[XmlRoot("doc")]
public class DocDateTime
{
    [XmlElement("dataNascita")]
    public DateTime DataNascita { get; set; }
}

[XmlRoot("doc")]
public class DocDateTimeOffset
{
    [XmlElement("dataNascita")]
    public DateTimeOffset DataNascita { get; set; }
}