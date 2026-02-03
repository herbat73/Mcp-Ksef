## Jak skonfigurować klienta

W zależności od tego czy uruchomiłeś serwer jako kontyner Docker czy z kodu należy skonfigurować klienta do serwera MCP

Domyślnie uruchomienie serwera jako konyner Docker będzie nasłuchiwał na porcie 8080 a z kodu na porcie 5280

### Konfiguracja dla serwera uruchomiony z Dockera (system produkcyjny)

```bash
docker run -e KSEF_TOKEN="tutaj twój token ksef" -e KSEF_VATID="tutaj wpisz number nip" -p 8080:8080 herbat73/mcp-ksef:latest --http --use-ksef-production 
```

Uruchom klienta MCP Visual Studio Code, konfiguracja mcp.json

```json
{
   "servers": {
      "my-mcp-ksef-http": {
         "type": "http",
         "url": "http://localhost:8080/mcp"
      }
   },
   "inputs": []
}
```

### Konfiguracja dla serwera uruchomionego z kodu (system produkcyjny)

Uruchom projekt do dowolnego środowiska np. test dla transportu HTTP strumieniowy

```bash
 dotnet run -e KSEF_TOKEN='tutaj wklej token ksef' -e KSEF_VATID='Tutaj NIP firmy' --project ./Mcp-Ksef.HybridApp -- --http -use-ksef-production 
```

Uruchom klienta MCP np. Visual Studio Code, konfiguracja mcp.json

```json
{
   "servers": {
      "my-mcp-ksef-http": {
         "type": "http",
         "url": "http://localhost:5280/mcp"
      }
   },
   "inputs": []
}
```

[Powrót do początku](../README.md)