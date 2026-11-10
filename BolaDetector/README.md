# BOLA Detector — SOLID i Clean Architecture

Modularni .NET 8 detektor za lokalni, kontrolisani REST API. Demo ne zahtijeva bazu, glavni Web API niti frontend. Odsustvo nalaza ne dokazuje sigurnost aplikacije.

## Pokretanje

Iz foldera `BolaDetector`:

```powershell
dotnet build BolaDetector.sln
dotnet test BolaDetector.sln
```

Prvi terminal — testni API:

```powershell
dotnet run --project targets/BolaTestApi --no-launch-profile --urls http://127.0.0.1:5081
```

Drugi terminal — detektor:

```powershell
$env:ALICE_TEST_USER = 'alice'
$env:BOB_TEST_USER = 'bob'
dotnet run --project src/BolaDetector.Demo -- configs/demo-detection.json reports/demo-report.json
```

Vrijednosti alice/bob su sintetički identiteti lokalnog API-ja. Kredencijali se razrješavaju iz `env:` reference tek pri zahtjevu i šalju u `X-Test-User`. Izvještaj ne čuva zaglavlja, kredencijale, sirovo tijelo odgovora niti izvorne poruke HTTP izuzetaka. Imena scenarija, korisnika, resursa i putanja u konfiguraciji moraju biti javni testni metapodaci bez tajni.

Argumenti su opcioni: bez njih koriste se kopija konfiguracije uz program i `bola-report.json` u tekućem folderu. Izlazni folder se kreira automatski. Ctrl+C prekida rad. Izlazni kodovi: 0 završen verifikovan demo (može uključivati nalaze), 1 greška konfiguracije/izvještaja, 2 ograničena verifikacija ili HTTP greška, 130 otkazivanje.

## Raspored projekta

```text
src/
  BolaDetector.Domain/
    Access/ Projects/ Resources/ Scenarios/ Findings/
  BolaDetector.Application/
    Abstractions/      # ugovori grupisani po ulozi
    Models/            # konfiguracija, izvršavanje, izvještaji
    UseCases/          # RunDetection
  BolaDetector.Engine/
    Authorization/ Scenarios/ Execution/
  BolaDetector.Infrastructure/
    Configuration/ DependencyInjection/ Http/ OpenApi/
    Persistence/ Reporting/ Security/ Serialization/ Verification/
  BolaDetector.Api/
    Endpoints/
  BolaDetector.Demo/
    Presentation/
  BolaDetector.Web/src/
    App.tsx main.tsx style.css
targets/BolaTestApi/
  Documents/ Endpoints/
tests/
  BolaDetector.UnitTests/
    Architecture/ Authorization/ Execution/ Findings/ Fixtures/ Http/ Scenarios/
  BolaDetector.IntegrationTests/
    Detection/ Reporting/
  BolaDetector.EndToEndTests/  # rezervisan projekat
configs/
reports/
docs/
```

Svaki javni produkcioni tip ima zaseban fajl. Domain nema projektnih zavisnosti. Application zavisi samo od Domain-a. Engine zavisi od Application/Domain i nema NuGet ni framework referenci. Infrastructure implementira portove, koristi Engine i sadrži HTTP, JSON, OpenAPI i EF detalje. Api i Demo biraju konkretne adaptere u composition root-u.

Namespace-i Domain/Application/Engine zadržani su gdje je moguće radi jednostavnije migracije; tehnički tipovi premješteni su u Infrastructure namespace. `TestExecution` i konfiguracioni/izvještajni DTO-i sada pripadaju Application-u. Promijenjen je konstruktor DetectionEngine-a; koristi DI ili njegove portove. `IDetectionEngine.ExecuteAsync`, JSON konfiguracija i format izvještaja ostali su sačuvani.

## Algoritam

1. `RunDetection` vodi kompletno izvršavanje preko `IScenarioGenerator` i `IDetectionEngine`.
2. `BolaScenarioGenerator` kombinuje operacije, kompatibilne resurse i sve identitete. Bira tačno jedno eksplicitno pravilo prema tipu resursa i akciji. Očekivanu dozvolu dobija od `IAuthorizationOracle`, koji delegira pravilu `AccessRule.Allows` u Domain-u: tip, vlasništvo i organizacija.
3. `DetectionEngine` orkestrira scenario preko `IScenarioRequestExecutor`: prvo zahtjev vlasnika za kontrolni odgovor, zatim zahtjev testiranog identiteta.
4. `HttpScenarioRequestExecutor` provjerava adresu, URL-kodira `{id}`, razrješava identitet i šalje zahtjev. Timeout, HTTP greške i preveliki odgovori imaju kontrolisane rezultate; otkazivanje pozivaoca se propagira.
5. `JsonReadEvidenceVerifier` provjerava uspješan kontrolni odgovor, JSON `id` i eksplicitna neprazna `protectedProperties`. Poredi ID i zaštićena polja; dokaz predstavlja njihov SHA-256 otisak bez sirovog sadržaja.
6. Domenski `FindingAnalyzer` klasifikuje nalaz. `JsonDetectionReportWriter` čuva javni rezultat, a `ConsoleReportPresenter` ga prikazuje.

Podržani su samo GET/read, jedan korak i jedan `{id}`. Dozvoljeno odredište je isključivo `http://127.0.0.1:5081`; produkciona DI registracija isključuje redirect, proxy i cookies. Timeout je 1–60 sekundi, a najveće tijelo odgovora 64 KiB. Složene JSON vrijednosti trenutno se porede tekstualno; odabrana zaštićena polja treba da budu stabilna tokom testa i da zaista sadrže zaštićene podatke.

## Klasifikacija i očekivani rezultat

| Kategorija | Značenje |
| --- | --- |
| ConfirmedViolation | Nedozvoljenom korisniku vraćeni su ID i zaštićena polja koja se podudaraju s provjerenim odgovorom vlasnika. |
| PotentialViolation | Uspješan status bez dovoljno dokaza o otkrivanju zaštićenih podataka. |
| LimitedVerification | Neuspjela kontrola, HTTP greška ili odgovor koji ne dozvoljava pouzdan zaključak. |
| NoViolationObserved | Dozvoljeno čitanje je provjereno ili je tuđi pristup odbijen bez dokazanog otkrivanja podataka. |

Status 200 sam nije dokaz. Ni status 403 ne isključuje nalaz ako tijelo sadrži dokazane zaštićene podatke. Sačuvan je analizator promjene stanja između dva čitljiva snimka istog resursa, ali HTTP izvršavanje izmjena još nije implementirano.

Demo generiše 2 operacije × 2 resursa × 2 identiteta = 8 scenarija: 4 pozitivna i 4 negativna. Sigurna ruta vlasnicima vraća 200, drugima 403. Ranjiva ruta vraća tuđe dokumente i daje dva ConfirmedViolation nalaza.

```text
bob    GET /secure/documents/doc-1      expected=False HTTP=403 NoViolationObserved
bob    GET /vulnerable/documents/doc-1  expected=False HTTP=200 ConfirmedViolation
alice  GET /vulnerable/documents/doc-2  expected=False HTTP=200 ConfirmedViolation
```

Stvarni izvještaj: [reports/demo-report.json](reports/demo-report.json).

## Provjere i ograničenja

Poslije refaktorisanja prolazi **37 jediničnih i 2 integraciona testa**, kao i .NET i React build. Arhitektonski testovi čuvaju smjer referenci i nezavisnost jezgra od frameworka. Testovi portova izvršavaju use-case bez HTTP-a/JSON-a i provjeravaju zamjenljivost saradnika. Ostali testovi provjeravaju kombinacije, vlasništvo, status, dokaz, greške, otkazivanje, ograničenje odgovora i zapis JSON izvještaja. Integracioni test pokreće pravi ASP.NET pipeline preko TestServer-a. EndToEndTests je prazan rezervisani projekat.

Nema mutacija, višekoračnih scenarija, replay/minimizacije, trajnog čuvanja nalaza ni pozadinskog reda. OpenAPI importer je sačuvan, ali demo koristi eksplicitne operacije iz JSON-a. YAML fajlovi su primjeri budućeg ugovora bez loadera. Testni header nije produkciona autentikacija. Naredna faza: izmjene uz nezavisne snimke i obnavljanje testnih podataka.

## Ostali dijelovi šablona

Glavni `BolaDetector.Api` ima MySQL/EF Core/Identity osnovu i nije potreban za demo. Za bazu i migracije:

```powershell
docker compose up -d mysql
dotnet tool install dotnet-ef --tool-path ./.tools --version 8.0.11
./.tools/dotnet-ef migrations add InitialCreate --project src/BolaDetector.Infrastructure --startup-project src/BolaDetector.Api --output-dir Persistence/Migrations
./.tools/dotnet-ef database update --project src/BolaDetector.Infrastructure --startup-project src/BolaDetector.Api
dotnet run --project src/BolaDetector.Api
```

`docker compose up --build -d` pokreće bazu, glavni API i testni API; demo se pokreće na hostu. Migracije nisu automatske. Docker/MySQL nisu provjeravani u ovom refaktorisanju. React šablon nije povezan s detektorom; pokretanje: `cd src/BolaDetector.Web`, `npm install`, `npm run dev`.

[Primjena SOLID principa i arhitektonske granice](docs/solid-architecture.md). [Istorijski zapis prve implementacije](docs/implementation-v1.md).
