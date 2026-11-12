using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace PocDotnetParseDate.App;

internal static class Program
{
    private static readonly string[] DefaultXmlTestCases =
    [
        "<doc><dataNascita>1942-02-08+02:00</dataNascita></doc>",
        "<doc><dataNascita>2001-07-01+02:00</dataNascita></doc>",
        "<doc><dataNascita>2001-02-08+01:00</dataNascita></doc>"
    ];

    public static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("================================================================================");
        Console.WriteLine("   POC .NET: Parsing Date XML con Offset e Analisi Day Shift (Generalizzato)   ");
        Console.WriteLine("================================================================================");
        Console.WriteLine($"Fuso orario locale macchina : {TimeZoneInfo.Local.DisplayName}");
        Console.WriteLine($"Offset fuso orario attuale  : {TimeZoneInfo.Local.GetUtcOffset(DateTime.Now)}\n");

        IEnumerable<string> inputs = args.Length > 0 ? args : DefaultXmlTestCases;

        int caseIndex = 1;
        foreach (var rawInput in inputs)
        {
            var xml = NormalizeXmlInput(rawInput);
            Console.WriteLine($"--------------------------------------------------------------------------------");
            Console.WriteLine($"CASO #{caseIndex++}: {xml}");
            Console.WriteLine($"--------------------------------------------------------------------------------");

            ProcessXmlCase(xml);
        }

        PrintFinalSummary();
    }

    /// <summary>
    /// Normalizza l'input: se viene passata una stringa data nuda (es. '2001-07-01+02:00')
    /// o un tag malformato (es. typo '<dov>'), la incapsula correttamente nel frammento XML atteso.
    /// </summary>
    private static string NormalizeXmlInput(string input)
    {
        var trimmed = input.Trim();

        // Se è già un XML ben formato con <dataNascita>
        if (trimmed.Contains("<dataNascita>", StringComparison.OrdinalIgnoreCase))
        {
            // Correzione eventuale typo tipo <dov>...</doc>
            if (trimmed.StartsWith("<dov", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = "<doc" + trimmed[4..];
            }
            return trimmed;
        }

        // Se è solo un valore di data (es. '1942-02-08+02:00'), costruisce l'XML
        return $"<doc><dataNascita>{trimmed}</dataNascita></doc>";
    }

    private static void ProcessXmlCase(string xml)
    {
        // Estrazione valore nominale dal frammento XML per determinare il giorno atteso
        string rawDateString = string.Empty;
        try
        {
            var xDoc = XDocument.Parse(xml);
            rawDateString = xDoc.Root?.Element("dataNascita")?.Value ?? string.Empty;
        }
        catch
        {
            // Fallback con Regex se l'XML è malformato
            var match = Regex.Match(xml, @"<dataNascita>(.*?)</dataNascita>", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                rawDateString = match.Groups[1].Value;
            }
        }

        if (string.IsNullOrWhiteSpace(rawDateString))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Impossibile estrarre l'elemento <dataNascita> dall'input fornito.\n");
            Console.ResetColor();
            return;
        }

        // Il giorno nominale indicato nel documento (senza conversioni di fuso)
        int expectedDay;
        if (DateTimeOffset.TryParse(rawDateString, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dtoExpected))
        {
            expectedDay = dtoExpected.Day;
        }
        else
        {
            // Tentativo parsing sottostringa ISO 8601 YYYY-MM-DD
            var parts = rawDateString.Split('-');
            if (parts.Length >= 3 && int.TryParse(parts[2][..2], out var parsedDay))
            {
                expectedDay = parsedDay;
            }
            else
            {
                expectedDay = -1;
            }
        }

        Console.WriteLine($"Valore testuale letto : \"{rawDateString}\" (Giorno nominale atteso: {expectedDay})\n");

        // -----------------------------------------------------------------------------
        // SEZIONE 1: Deserializzazione tramite System.Xml.Serialization.XmlSerializer
        // -----------------------------------------------------------------------------
        Console.WriteLine("--- 1. XmlSerializer ---");

        // Approccio Standard A: System.DateTime
        try
        {
            var serializerA = new XmlSerializer(typeof(DocDateTime));
            using var readerA = new StringReader(xml);
            if (serializerA.Deserialize(readerA) is DocDateTime docA)
            {
                PrintResult(
                    label: "Approccio A (System.DateTime)",
                    formattedDate: docA.DataNascita.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
                    day: docA.DataNascita.Day,
                    expectedDay: expectedDay,
                    extraDetails: $"DateTimeKind = {docA.DataNascita.Kind} (convertito automaticamente all'ora locale)");
            }
        }
        catch (Exception ex)
        {
            PrintError("Approccio A (XmlSerializer)", ex.Message);
        }

        // Approccio Consigliato B: System.DateTimeOffset
        try
        {
            var serializerB = new XmlSerializer(typeof(DocDateTimeOffset));
            using var readerB = new StringReader(xml);
            if (serializerB.Deserialize(readerB) is DocDateTimeOffset docB)
            {
                PrintResult(
                    label: "Approccio B (System.DateTimeOffset)",
                    formattedDate: docB.DataNascita.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
                    day: docB.DataNascita.Day,
                    expectedDay: expectedDay,
                    extraDetails: $"Offset = {docB.DataNascita.Offset} (preserva l'offset originale senza shift)");
            }
        }
        catch (Exception ex)
        {
            PrintError("Approccio B (XmlSerializer)", ex.Message);
        }

        // -----------------------------------------------------------------------------
        // SEZIONE 2: Parsing alternativo con XDocument e DateTimeStyles
        // -----------------------------------------------------------------------------
        Console.WriteLine("--- 2. XDocument & Parsing Diretto ---");

        // Parsing diretto su DateTime
        if (DateTime.TryParse(rawDateString, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dtStandard))
        {
            PrintResult(
                label: "XDocument -> DateTime.Parse",
                formattedDate: dtStandard.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
                day: dtStandard.Day,
                expectedDay: expectedDay,
                extraDetails: $"DateTimeKind = {dtStandard.Kind}");
        }

        // Parsing con DateTimeStyles.RoundtripKind
        if (DateTime.TryParse(rawDateString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dtRoundtrip))
        {
            PrintResult(
                label: "XDocument -> DateTime.Parse (RoundtripKind)",
                formattedDate: dtRoundtrip.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
                day: dtRoundtrip.Day,
                expectedDay: expectedDay,
                extraDetails: $"DateTimeKind = {dtRoundtrip.Kind} (se con offset != UTC, DateTime converte comunque in Local)");
        }

        // Parsing verso DateTimeOffset
        if (DateTimeOffset.TryParse(rawDateString, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dtoDirect))
        {
            PrintResult(
                label: "XDocument -> DateTimeOffset.Parse",
                formattedDate: dtoDirect.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
                day: dtoDirect.Day,
                expectedDay: expectedDay,
                extraDetails: $"Offset = {dtoDirect.Offset} (raccomandato per preservare il valore originale)");
        }

        Console.WriteLine();
    }

    private static void PrintResult(string label, string formattedDate, int day, int expectedDay, string extraDetails)
    {
        Console.WriteLine($"  [{label}]");
        Console.WriteLine($"    Data/Ora formattata : {formattedDate}");
        Console.WriteLine($"    Giorno (.Day)       : {day}");
        Console.WriteLine($"    Dettagli            : {extraDetails}");

        Console.Write("    Esito               : ");
        var originalColor = Console.ForegroundColor;
        if (expectedDay != -1 && day == expectedDay)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"OK: Il giorno è preservato correttamente ({day})");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"ATTENZIONE: Valore scalato al giorno {day} anziché {expectedDay}! (Shift causato da conversione a ora locale/DST)");
        }
        Console.ForegroundColor = originalColor;
        Console.WriteLine();
    }

    private static void PrintError(string context, string message)
    {
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  Errore in [{context}]: {message}");
        Console.ForegroundColor = originalColor;
    }

    private static void PrintFinalSummary()
    {
        Console.WriteLine("================================================================================");
        Console.WriteLine("ANALISI E CONFRONTO TECNICO TRA LE DATE:");
        Console.WriteLine("1. Caso Invernale con offset +02:00 (1942-02-08+02:00):");
        Console.WriteLine("   - A febbraio in Italia/Europa (CET) l'ora solare ha offset UTC+01:00.");
        Console.WriteLine("   - 1942-02-08 00:00:00+02:00 convertito in ora locale (+01:00) scala a 1942-02-07 23:00:00.");
        Console.WriteLine("   - Risultato: .Day si sposta all'indietro a 7 (BUG critico su date di nascita!).");
        Console.WriteLine();
        Console.WriteLine("2. Caso Estivo con offset +02:00 (2001-07-01+02:00):");
        Console.WriteLine("   - A luglio in Italia/Europa (CEST) l'ora legale (DST) ha offset UTC+02:00.");
        Console.WriteLine("   - 2001-07-01 00:00:00+02:00 coincide con l'offset estivo locale, quindi .Day resta 1;");
        Console.WriteLine("     tuttavia scalerebbe al 30 giugno se eseguito su server o container in UTC (UTC+0).");
        Console.WriteLine();
        Console.WriteLine("3. Caso Invernale con offset standard +01:00 (2001-02-08+01:00):");
        Console.WriteLine("   - A febbraio l'offset solare standard (CET) è UTC+01:00.");
        Console.WriteLine("   - 2001-02-08 00:00:00+01:00 coincide esattamente con l'offset solare della macchina locale.");
        Console.WriteLine("   - Risultato: .Day resta 8 sia con DateTime sia con DateTimeOffset.");
        Console.WriteLine();
        Console.WriteLine("CONCLUSIONE:");
        Console.WriteLine("   Con 'System.DateTime', la correttezza del giorno dipende in modo aleatorio dalla");
        Console.WriteLine("   coincidenza tra l'offset nel documento e le regole DST/fuso orario locale.");
        Console.WriteLine("   'System.DateTimeOffset' elimina qualsiasi ambiguità preservando sempre il giorno nominale.");
        Console.WriteLine("================================================================================");
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