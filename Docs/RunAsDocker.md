## Uruchomienie jako Docker

Wymaga wcześniejsz instalacji [Docker](https://www.docker.com/) na komputerze na którym ma działać serwer MCP.

1. Image Docker jest gotowy do ściągnięcia i instalacji w publiczny repozytorium Docker Hub jako [herbat73/mcp-ksef](https://hub.docker.com/repository/docker/herbat73/mcp-ksef/)

2. Uruchomienie

**Przykładowe komendy**

- system testowy KSeF

```bash
docker run -e KSEF_TOKEN="tutaj twój token ksef" -e KSEF_VATID="tutaj wpisz number nip" -p 8080:8080 herbat73/mcp-ksef:latest --http
```
- system produkcyjny KSeF

```bash
docker run -e KSEF_TOKEN="tutaj twój token ksef" -e KSEF_VATID="tutaj wpisz number nip" -p 8080:8080 herbat73/mcp-ksef:latest --http --use-ksef-production 
```

### Parametry

- `KSEF_VATID`: - NIP firmy dla której chcesz użyć systemu KsEF
- `KSEF_CERTIFICATE_FILE`: - ścieżka do certyfikatu KSeF
- `KSEF_PRIVATE_KEY_FILE`: - ścieżka do klucz prywatnego wygenerowanego w systemie KSeF
- `KSEF_PRIVATE_KEY_PASSWORD`: - hasło do klucza prywatnego podanego do generacji klucza prywatnego w KSeF
  lub
- `KSEF_TOKEN`: - token KSeF wygenerowany w systemie KsEF (dla wybranego systemu Test lub Produkcja).

**Przełączniki**

**--http** - tryb transportowy http

**--use-ksef-production** - tryb produkcujny systemu KsEF (bez przełącznika podłącza się do systemu testowego KSeF)

## Uruchomienie z docker-compose

Przykładowy plik jest tutaj [compose.yaml](../compose.yaml)

```bash
services:
    mcpksef:
    image: herbat73/mcp-ksef:latest
    container_name: mcp-ksef-container
    ports:
      - 8080:8080
    command: --http --use-ksef-production
    environment:
      - KSEF_TOKEN=Tutaj_Wklej_Token_KSeF
      - KSEF_VATID=Tutaj_Wpisz_Numer_Vat_Swojej_firmy
```

aby go uruchomić zmienć parametry KSEF_TOKEN oraz KSEF_VATID a następnie wpisz komendę 

```bash
docker-compose -f compose.yaml up -d
```

## Uruchomienie z docker-compose z certyfikatem KSeF

Przykładowy plik jest tutaj [compose_with_cert.yaml](../compose_with_cert.yaml

```bash
services:
  mcpksef:
    image: herbat73/mcp-ksef:latest
    container_name: mcp-ksef-container
    volumes:
      - "C:/Users/aadam/Projects/Certs:/etc/certs:ro"
    ports:
      - 8080:8080
    command: --http --use-ksef-production
    environment:
      - KSEF_CERTIFICATE_FILE=/etc/certs/Cert3.crt
      - KSEF_PRIVATE_KEY_FILE=/etc/certs/Cert3.key
      - KSEF_PRIVATE_KEY_PASSWORD=Tutaj_haslo_do_klucza_prywatnego
      - KSEF_VATID=Tutaj_Wpisz_Numer_Vat_Swojej_firmy
```

Gdzie w tym przypadku

- C:/Users/aadam/Projects/Certs to miejsce gdzie znajdują się certyfikat KSeF i klucz prywatny na komputerze.
- Cert3.crt - to nazwa pliku certyfikatu jaki został pobrany z KSeF
- Cert3.key - to nazwa pliku klucza prywatnego jaki został użyty do generowania certyfikatu

Zmodyfikuj parametry do swoich nazw ceryfikatu, kluczy, hasła, nip i wykonaj komendę

```bash
 docker-compose -f compose_with_cert.yaml up -d
```

[Powrót do początku](../README.md)
