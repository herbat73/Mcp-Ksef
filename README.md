# MCP KSeF Server

To jest serwer MCP dla KSeF - Krajowy System e-Faktur to platforma do wystawiania, przesyłania, otrzymywania i przechowywania faktur.

## Instalacja

[![Install in VS Code](https://img.shields.io/badge/VS_Code-Install-0098FF?style=flat-square&logo=visualstudiocode&logoColor=white)](https://vscode.dev/redirect?url=vscode%3Amcp%2Finstall%3F%7B%22name%22%3A%22markdown-to-html%22%2C%22gallery%22%3Afalse%2C%22command%22%3A%22docker%22%2C%22args%22%3A%5B%22run%22%2C%22-i%22%2C%22--rm%22%2C%22ghcr.io%2Fmicrosoft%2Fmcp-dotnet-samples%2Fmarkdown-to-html%3Alatest%22%5D%7D) [![Install in VS Code Insiders](https://img.shields.io/badge/VS_Code_Insiders-Install-24bfa5?style=flat-square&logo=visualstudiocode&logoColor=white)](https://insiders.vscode.dev/redirect?url=vscode-insiders%3Amcp%2Finstall%3F%7B%22name%22%3A%22markdown-to-html%22%2C%22gallery%22%3Afalse%2C%22command%22%3A%22docker%22%2C%22args%22%3A%5B%22run%22%2C%22-i%22%2C%22--rm%22%2C%22ghcr.io%2Fmicrosoft%2Fmcp-dotnet-samples%2Fmarkdown-to-html%3Alatest%22%5D%7D) [![Install in Visual Studio](https://img.shields.io/badge/Visual_Studio-Install-C16FDE?logo=visualstudio&logoColor=white)](https://aka.ms/vs/mcp-install?%7B%22name%22%3A%22markdown-to-html%22%2C%22gallery%22%3Afalse%2C%22command%22%3A%22docker%22%2C%22args%22%3A%5B%22run%22%2C%22-i%22%2C%22--rm%22%2C%22ghcr.io%2Fmicrosoft%2Fmcp-dotnet-samples%2Fmarkdown-to-html%3Alatest%22%5D%7D)

## Wymagania

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Visual Studio Code](https://code.visualstudio.com/) with
  - [C# Dev Kit](https://marketplace.visualstudio.com/items/?itemName=ms-dotnettools.csdevkit) extension

## Co zawiera

Markdown to HTML MCP server includes:

| Building Block | Name                     | Description                                     | Usage                       |
|----------------|--------------------------|-------------------------------------------------|-----------------------------|
| Tools          | `get_invoice_by_ksef`    | Pobranie faktury w XML na podstawie numeru KSeF | `#get_invoice_by_ksef` |

## Jak to użyć

### Uruchamianie serwera MCP

#### Na maszynie lokalnej

1. Uruchomienie serwera MCP.

    ```bash
    cd $REPOSITORY_ROOT/Mcp-Ksef.HybridApp
    dotnet run --project ./Mcp-Ksef.HybridApp
    ```
   **Parametry**:

   - `--http`: Przełącznik wskazujący, że serwer MCP ma działać jako serwer strumieniowy HTTP. Po dodaniu tego przełącznika adres URL serwera MCP będzie następujący: `http://localhost:5280`.
   - `--use-ksef-production`: Przełącznik wskazujący czy należy użyć serwera produkcyjnego, domyślnie bez przełącznika MCP użyje serwera testowego.


   **Zmienne środowiskowe**:
   
   Ustaw zmienne środowiskowe potrzebne do dostępu do KSeF

   - `KSEF_TOKEN`: - token KSeF wygenerowany w systemie KsEF (dla wybranego systemu Test lub Produkcja).
   - `KSEF_VATID`: - NIP firmy dla której chcesz użyć systemu KsEF

   Z tymi parametrami możesz użyć serwera MCP jak:

   ```bash
   dotnet run --project ./Mcp-Ksef.HybridApp -- --http --use-ksef-production
   ```
