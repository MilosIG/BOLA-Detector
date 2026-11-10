# Implementacija v1 ? promjene i validacija

Istorijski zapis prije SOLID refaktorisanja. Aktuelne lokacije, granice i ugovori opisani su u [solid-architecture.md](solid-architecture.md) i README-u.

## Dodati fajlovi

- src/BolaDetector.Domain/DetectionModels.cs
- src/BolaDetector.Engine/BolaScenarioGenerator.cs
- src/BolaDetector.Engine/DetectionEngine.cs
- src/BolaDetector.Engine/DetectionServices.cs
- src/BolaDetector.Demo/BolaDetector.Demo.csproj
- src/BolaDetector.Demo/Program.cs
- configs/demo-detection.json
- tests/BolaDetector.UnitTests/DetectionTests.cs
- tests/BolaDetector.IntegrationTests/DetectionIntegrationTests.cs
- reports/demo-report.json (izlaz stvarnog lokalnog demo izvr?avanja)
- docs/implementation-v1.md

## Izmijenjeni fajlovi

- BolaDetector.sln ? dodat konzolni demo.
- src/BolaDetector.Domain/Models.cs ? opcioni kontekst scenarija i rezultat/nalaz izvr?avanja uz sa?uvane konstruktore.
- src/BolaDetector.Application/Contracts.cs ? ITestCredentialProvider.
- src/BolaDetector.Engine/BolaDetector.Engine.csproj ? referenca shared frameworka za DI i HttpClient.
- src/BolaDetector.Engine/FindingAnalyzer.cs ? analiza ?itanja i pouzdanosti snimaka.
- targets/BolaTestApi/Program.cs ? dva dokumenta, provjera stvarnog vlasnika, imenovani ulaz za integracione testove.
- tests/BolaDetector.UnitTests/BolaDetector.UnitTests.csproj ? kopiranje demo konfiguracije.
- tests/BolaDetector.IntegrationTests/BolaDetector.IntegrationTests.csproj ? engine/target reference, test host paket i konfiguracija.
- tests/BolaDetector.IntegrationTests/README.md ? opis aktivnih testova.
- README.md ? algoritam, pokretanje, klasifikacija i ograni?enja.

## Odluke

Modeli su pro?ireni init svojstvima; postoje?i IDetectionEngine i konstruktori ostali su kompatibilni. Demo je zasebna konzolna kompozicija istih modula, zato nema zavisnosti od MySQL-a niti glavnog Web API-ja. Generator radi kartezijanski proizvod operacija, kompatibilnih resursa i identiteta, bira eksplicitno pravilo i poziva oracle. Izvr?iva? pribavlja vlasnikov kontrolni odgovor prije odgovora testiranog identiteta. Potvrda ?itanja zavisi od odgovaraju?eg ID-ja i podudaranja eksplicitnih za?ti?enih polja, a ne od statusa. Izvje?taj zadr?ava metapodatke i SHA-256 otisak bez tijela odgovora i zaglavlja.

## Izvr?ene provjere

- `dotnet build BolaDetector.sln --no-restore`: uspjeh, 0 upozorenja, 0 gre?aka.
- `dotnet test BolaDetector.sln --no-build`: 29 unit + 1 integracioni test, svi uspje?ni.
- EndToEndTests je rezervisani prazan projekat i ne doprinosi broju testova.
- Pokrenut pravi BolaTestApi na 127.0.0.1:5081 i konzolni detektor: osam scenarija, ?est NoViolationObserved, dvije ConfirmedViolation; izlazni kod 0.
- Sigurna ruta: oba vlasnika 200, oba tu?a pristupa 403; ranjiva ruta: oba tu?a pristupa 200 i potvr?eno podudaranje za?ti?enog sadr?aja.
- Sa?uvan stvarni JSON izvje?taj u reports/demo-report.json.
- Docker i MySQL nisu dio ove provjere; frontend nije mijenjan.

Preporu?ena naredna faza je podr?ka izmjenama uz vlasnikove snimke stanja prije/poslije, izolovano resetovanje testnih podataka i replay. Time se mo?e dokazivati promjena stanja nezavisno od statusa, bez pro?irivanja na proizvoljna odredi?ta.
