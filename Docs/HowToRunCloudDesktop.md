## Jak skonfigurować klienta Cloud Desktop

Przykład konfiguracji dla [Cloud Desktop](https://claude.com/download)

Uruchom klienta Cloud Desktop i przejdź do ustawień (Settings).

![Cloud Desktop](../Images/CloudDesktopSettings.png)

Kliknij na ustawienia dewelopera (Developer)

![Cloud Desktop](../Images/CloudDesktopSettingsEditConfig.png)

Następnie zmień plik konfiguracji claude_desktop_config.json na


```json
{
  "mcpServers": {
    "my-server": {
      "command": "npx",
      "args": ["mcp-remote", "http://localhost:5280/mcp"]
    }
  }
}
```

zakładając ze uruchomiłeś MCP na porcie 5280 (lub jako Docker na 8080 zmieniając wartość).

Zamknikj Cloud Desktop i uruchom ponownie.

Cloud Desktop powinen mieć możliwość współpracy z MCP i KsEF.

Wpisz np. 

`
Pobierz faktury z podanego okresu od 2026.01.01 do 2026.02.01
`

![Cloud Desktop](../Images/CloudDesktop_Pobierz_Faktury.png)

Cloud powinien pobrać faktury.

Zawuważ, źe Cloud Desktop trzyma kontekst i możes dalej danymi faktur robić co chcesz np.

`
Pobierz faktury z podanego okresu od 2026.01.01 do 2026.02.01
`

![Cloud Desktop](../Images/CloudDesktop_Podsumuj_Faktury.png)

[Powrót do początku](../README.md)