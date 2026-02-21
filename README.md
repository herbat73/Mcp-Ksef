# MCP KSeF Server

[![](https://badge.mcpx.dev?status=on 'MCP Enabled')](https://modelcontextprotocol.io/introduction)
[![](https://img.shields.io/badge/License-MIT-red.svg 'MIT License')](https://opensource.org/licenses/MIT)
[![Build](https://github.com/herbat73/Mcp-Ksef/actions/workflows/dotnet_build_and_test.yml//badge.svg)](https://github.com/herbat73/Mcp-Ksef/actions/workflows/dotnet_build_and_test.yml)
[![DockerPull](https://img.shields.io/docker/pulls/herbat73/mcp-ksef)](https://hub.docker.com/r/herbat73/mcp-ksef)

To jest serwer MCP dla KSeF - Krajowy System e-Faktur to platforma do wystawiania, przesyłania, otrzymywania i przechowywania faktur.

Połączenie do KSeF wymaga wygenerowania tokenu lub certyfikatu. Obie metody są wspierane przez ten serwer MCP.

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

| Opis                                                  | Uzycie                 |
|-------------------------------------------------------|------------------------|
| Pobranie faktury w XML na podstawie numeru KSeF       | `#get_invoice_by_ksef` |
| Pobiera listę faktur z podanego okresu z systemu ksef | `#get_invoices_for_period` |
| Pobiera fakturę o podanym numerze faktury (*)         | `#get_invoice_by_invoice_number` |
| Pobierz faktury dla kupującego o numerze NIP (*)      | `#get_invoices_for_buyer_by_nip` |
| Pobierz faktury dla kupującego o numerze VAT UE (*)   | `#get_invoices_for_buyer_by_vateu` |
| Pobierz link do faktury po numerze ksef               | `#get_invoice_url_by_ksef` |
| Pobierz QR kod do faktury po numerze ksef             | `#get_invoice_qr_ksef` |

(*) repozytorium KSeF ma ograniczenie na zwrot listy faktur do maksymalnie 3 miesiące wstecz

## Jak użyć

Użyj podobnego opisu lub użyj bezpośrednio nazwy w celu wywołania pożądanej funkcji.

Np.

```bash
Pobierz fakturę o numerze referencyjnym 5242764991-20260131-01002063FA88-AD i pokaż dane identyfkacyjne podmiotu2 oraz number faktury
```

Lub bezpośrednio
```bash
#get_invoice_by_invoice_number "FV 2026/1/31/754149652576"
```

## Przykład

Przykład użycia połączenia do KSeF w kliencie Claude Desktop

<p>
  <img src="./Images/pobierz_fakture_po_numerze_ksef.gif" alt="KSeF MCP na Claude Desktop " width="800">
</p>