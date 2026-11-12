# POC .NET: Parsing Date XML con Offset e Analisi del Day Shift

Questo repository contiene un'applicazione Proof of Concept (POC) in .NET 8 / C# progettata per analizzare, dimostrare e prevenire un comune ma insidioso bug di corruzione dati legato alla deserializzazione di date XML con fuso orario/offset.

In particolare, il progetto evidenzia come l'utilizzo di `System.DateTime` anziché `System.DateTimeOffset` per deserializzare date di nascita XML (es. `<doc><dataNascita>1942-02-08+02:00</dataNascita></doc>`) possa provocare lo **slittamento indesiderato della data al giorno precedente** a causa delle regole di conversione al fuso orario locale o UTC.

---

## 1. Il Contesto Storico: L'Ora Legale di Guerra del 1942 in Italia

Nel frammento XML analizzato:
```xml
<doc><dataNascita>1942-02-08+02:00</dataNascita></doc>
```
il valore temporale presenta una data nel mese di febbraio con offset esplicito `+02:00`.

A prima vista, per un osservatore moderno un offset `+02:00` a febbraio in Italia potrebbe apparire anomalo, poiché il fuso orario standard invernale italiano (CET - *Central European Time*) è `UTC+01:00`, mentre l'ora legale estiva (CEST - *Central European Summer Time*, `UTC+02:00`) entra solitamente in vigore a fine marzo.

Tuttavia, **il 1942 in Italia rappresentò una precisa eccezione storica**:
- Durante la Seconda Guerra Mondiale, con il **Regio Decreto Legge n. 660 del 14 giugno 1940** e successivi provvedimenti governativi, l'Italia introdusse la cosiddetta **"ora legale di guerra"** allo scopo di massimizzare il risparmio energetico e l'utilizzo delle ore diurne.
- Questa disposizione rimase in vigore **in modo continuativo anche durante i mesi invernali del 1940, 1941 e per quasi tutto il 1942**, senza essere revocata nei mesi freddi.
- Di conseguenza, nel mese di **febbraio 1942 l'ora civile ufficiale in Italia era effettivamente UTC+02:00**.

> [!IMPORTANT]
> Il valore `1942-02-08+02:00` è un dato **storicamente e anagraficamente autentico e corretto**: a mezzanotte dell'8 febbraio 1942 a Roma, l'orologio segnava le 00:00 su un fuso con offset `+02:00`.
> Lo shift al giorno 7 **non è un errore del dato XML in input**, bensì un vero e proprio **errore di parsing applicativo** generato dall'adozione di un tipo di dato non idoneo (`System.DateTime`).

---

## 2. Perché `System.DateTime` Causa un Errore di Parsing

Quando si effettua la deserializzazione XML (tramite `System.Xml.Serialization.XmlSerializer` o parsing con `System.DateTime.Parse`):

1. **Assenza dell'offset in `System.DateTime`**:  
   La struttura `System.DateTime` non memorizza l'offset numerico originale (`+02:00`), ma possiede unicamente la proprietà `Kind` (`DateTimeKind.Utc`, `DateTimeKind.Local` o `DateTimeKind.Unspecified`).
2. **Conversione automatica all'ora locale**:  
   Gli standard XML Schema (`xs:dateTime` / `xs:date`) definiscono la deserializzazione verso `System.DateTime` convertendo automaticamente l'istante al fuso orario locale della macchina host (`DateTimeKind.Local`).
3. **Mancata applicazione delle regole storiche da parte del sistema operativo**:  
   I database dei fusi orari di Windows (e di vari sistemi operativi moderni) spesso applicano regole semplificate o attuali retroattivamente per date molto lontane nel tempo, trattando febbraio come fuso solare standard (`UTC+01:00`).
4. **Il calcolo dello shift**:  
   - Mezzanotte locale con offset `+02:00`: `1942-02-08 00:00:00+02:00`
   - Conversione a UTC: `1942-02-07 22:00:00Z`
   - Conversione al fuso locale host (es. CET `UTC+01:00`): **`1942-02-07 23:00:00+01:00`**
   - Accesso alla proprietà giorno: **`.Day == 7` anziché `8`!**

Lo stesso problema si manifesta sistematicamente se l'applicazione viene eseguita su un server cloud, container Linux (Docker) o pipeline CI/CD configurata su fuso **UTC (`UTC+00:00`)**: in quel caso l'istante diventa `1942-02-07 22:00:00 UTC`, scalando ugualmente al giorno 7.

---

## 3. Scenari a Confronto nel Progetto

La POC confronta tre casi emblematici per dimostrare come il comportamento di `DateTime` sia aleatorio e dipendente dalla stagione e dal fuso:

| Test Case | XML in Input | Stagione / Offset Documento | Comportamento con `DateTime` (Approccio A) | Comportamento con `DateTimeOffset` (Approccio B) | Esito e Diagnosi |
| :--- | :--- | :---: | :---: | :---: | :--- |
| **Caso #1** | `<dataNascita>1942-02-08+02:00</dataNascita>` | Inverno (`+02:00` Ora di guerra) | `1942-02-07 23:00` → **`.Day = 7`** | `1942-02-08 00:00` → **`.Day = 8`** | <span style="color:red">**BUG**</span>: Data scalata al giorno precedente. |
| **Caso #2** | `<dataNascita>2001-07-01+02:00</dataNascita>` | Estate (`+02:00` Ora legale CEST) | `2001-07-01 00:00` → **`.Day = 1`** | `2001-07-01 00:00` → **`.Day = 1`** | <span style="color:orange">**FALSO POSITIVO**</span>: Funziona in locale solo perché l'ora legale coincide con `+02:00`; su server UTC scalerebbe al 30 giugno! |
| **Caso #3** | `<dataNascita>2001-02-08+01:00</dataNascita>` | Inverno (`+01:00` Ora solare CET) | `2001-02-08 00:00` → **`.Day = 8`** | `2001-02-08 00:00` → **`.Day = 8`** | <span style="color:green">**ALLINEATO**</span>: L'offset del documento coincide con l'offset solare invernale. |

---

## 4. Soluzione Raccomandata

### Modello di Deserializzazione con `System.DateTimeOffset`

In .NET moderno (incluso .NET 8), `System.Xml.Serialization.XmlSerializer` supporta pienamente `System.DateTimeOffset`:

```csharp
[XmlRoot("doc")]
public class DocDateTimeOffset
{
    [XmlElement("dataNascita")]
    public DateTimeOffset DataNascita { get; set; }
}
```

Utilizzando `DateTimeOffset`:
- L'offset originale (`+02:00`) viene **preservato integralmente**.
- L'orario resta `00:00:00`.
- La proprietà `.Day` restituisce sempre il **giorno nominale esatto (`8`)**, a prescindere dal fuso orario della macchina su cui viene eseguito il codice e senza risentire delle regole DST.

> [!TIP]
> In scenari applicativi dove conta unicamente la data di calendario senza orario (come le date di nascita), da .NET 6 in poi è possibile valutare anche il tipo `System.DateOnly` per isolare completamente il concetto di data anagrafica da qualsiasi componente oraria o fuso orario.

---

## 5. Come Eseguire la POC

### Prerequisiti
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Esecuzione della Suite Predefinita
Esegue automaticamente i 3 casi di test descritti sopra:
```powershell
dotnet run
```

### Esecuzione con Argomento Arbitrario
È possibile passare qualsiasi frammento XML o valore di data direttamente da riga di comando:
```powershell
# Frammento XML completo:
dotnet run -- "<doc><dataNascita>1990-01-01+03:00</dataNascita></doc>"

# Oppure semplicemente la stringa data:
dotnet run -- "1942-02-08+02:00"
```
