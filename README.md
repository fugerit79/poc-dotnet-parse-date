# Date parsing POC con .NET

Vedi il [README.md](PocDotnetParseDate/README.md)

## Quickstart in GitHub Codespaces

Il repository include la configurazione devcontainer pronta all'uso (`.devcontainer/devcontainer.json`):
1. Dalla pagina GitHub del repository, clicca su **Code** > scheda **Codespaces** > **Create codespace on main**.
2. Il container predispone automaticamente **.NET 8 SDK** e le estensioni C# per VS Code.
3. Nel terminale di Codespaces, esegui:
   ```bash
   cd PocDotnetParseDate
   dotnet run
   ```
> [!TIP]
> Poiché i container GitHub Codespaces girano di default con fuso orario **UTC**, eseguendo l'app in Codespaces si potrà osservare dal vivo come anche il caso estivo (`2001-07-01+02:00`) scali al 30 giugno con `DateTime`, mentre con `DateTimeOffset` il giorno resta invariato a 1.  
> Per simulare il fuso di Roma dentro Codespaces è sufficiente impostare la variabile `TZ`:
> ```bash
> TZ="Europe/Rome" dotnet run
> ```
