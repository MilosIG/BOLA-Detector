# Procesni E2E testovi

Iz direktorijuma `BolaDetector` pokreni:

```powershell
dotnet build BolaDetector.sln -c Release
dotnet test tests/BolaDetector.EndToEndTests/BolaDetector.EndToEndTests.csproj -c Release --no-build
```

Potreban je .NET 8, izvorni checkout sa standardnim izlaznim direktorijumima
builda i mogućnost pokretanja procesa koji slušaju na loopback adresi.
Testovi pokreću kompajlirane DLL-ove kroz .NET host. Zavisnosti u testnom
projektu služe redoslijedu builda. Testovi ne učitavaju aplikacione sklopove
u NUnit proces.

- Autorizovani CRUD pokreće vlastiti `BolaTestApi` na portu koji dodijeli
  Kestrel. Koristi ID i `Location` iz POST odgovora. Provjerava vlasnika,
  čitanje, izmjenu, brisanje i konačni HTTP 404.
- Autorizovani Read pokreće vlastiti API i Demo uz jednu sigurnu GET
  operaciju vlasnika. Potreban je slobodan `127.0.0.1:5081`, koji propisuje
  postojeća politika detektora. Zauzet port uzrokuje jasan neuspjeh testa.
  Postojeći proces ostaje aktivan. Test provjerava izlaz 0 i JSON izvještaj.
- Nevalidna konfiguracija pokreće samo Demo. Test provjerava izlaz 1,
  validacioni kod, putanju, odsustvo sintetičke vrijednosti kredencijala
  u izlazu i odsustvo izvještaja.

`LocalRegressionTests` se izvršava serijski. `LocalApiProcess` koristi
objavu adrese iz stdout-a i HTTP `/health` za spremnost. Spremnost ima rok
12 sekundi, HTTP zahtjevi 5 sekundi, a Demo 15 sekundi za izlaz.
`TestProcess` prikuplja stdout i stderr za dijagnostiku. Po završetku se
zaustavljaju samo procesi koje je test pokrenuo. CRUD pokušava obrisati
kreirani dokument i pri neuspjehu testa, a zaustavljanje vlastitog API-ja
uklanja preostalo stanje iz memorije. Svaki test briše vlastiti privremeni
direktorijum.

MySQL, Docker i eksterni servisi nisu potrebni. Kompletna Read regresija
od osam scenarija ostaje u integracionim testovima s TestServer-om.
