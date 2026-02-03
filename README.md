# MCP KSeF Server

To jest serwer MCP dla KSeF - Krajowy System e-Faktur to platforma do wystawiania, przesyłania, otrzymywania i przechowywania faktur.

**Serwer MCP w rozwoju. Na razie działa tylko pobieranie faktur. Będą dodawane nowe funkcje.**

Spis treści

* [Jak uruchomić jako Docker](Docs/RunAsDocker.md)
* [Jak uruchomić klienta Visual Studio Code](Docs/HowToRun.md)
* [Jak uruchomić klienta Cloud Desktop](Docs/HowToRunCloudDesktop.md)
* [Przykłady użycia](Docs/Examples.md)
* [Punkty dostępu](Docs/Endpoints.md)
* [Instalacja z kodu](Docs/Installation.md)

## Co zawiera

Instrukcje dla KSeF

| Building Block | Name                     | Opis                                                  | Uzycie                 |
|----------------|--------------------------|-------------------------------------------------------|------------------------|
| Tools          | `get_invoice_by_ksef`    | Pobranie faktury w XML na podstawie numeru KSeF       | `#get_invoice_by_ksef` |
| Tools          | `get_invoices_for_period`| Pobiera listę faktur z podanego okresu z systemu ksef | `#get_invoices_for_period` |
