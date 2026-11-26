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

Svako izvršavanje validira privatnu kopiju ulazne konfiguracije. Generator,
izvršilac zahtjeva i verifikator koriste isti nepromjenljiv snimak sesije.
Neispravan ulaz daje izlazni kod `1` uz kod greške i putanju do polja.
[Pravila i kodovi validacije](docs/implementation-v3-configuration-validation.md).
[Tok snimka i promjene ugovora](docs/architecture-audit.md#nepromjenljiv-snimak-konfiguracije-po-sesiji).

Argumenti su opcioni: bez njih koriste se kopija konfiguracije uz program i `bola-report.json` u tekućem folderu. Izlazni folder se kreira automatski. Ctrl+C prekida rad. Izlazni kodovi: 0 završen verifikovan demo (može uključivati nalaze), 1 greška konfiguracije/izvještaja, 2 ograničena verifikacija ili HTTP greška, 130 otkazivanje.

## Lokalni CRUD dokumenata

Pokreni `BolaTestApi` direktno na `127.0.0.1:5081` prema komandi iznad. CRUD
rute prihvataju direktne loopback veze i jedno zaglavlje `X-Test-User` sa
vrijednošću `alice` ili `bob`. Docker bridge nije podržan za ove rute.

| Metoda i ruta | Zahtjev | Uspjeh |
| --- | --- | --- |
| `POST /documents` | JSON `{"title":"Novi dokument"}` | `201 Created`, dokument i `Location` |
| `GET /documents/{id}` | Identitet vlasnika | `200 OK` i dokument |
| `PUT /documents/{id}` | Identitet vlasnika i JSON `{"title":"Izmijenjen naslov"}` | `204 No Content` |
| `DELETE /documents/{id}` | Identitet vlasnika | `204 No Content` |

API generiše identifikator i dodjeljuje vlasnika iz zaglavlja. Naslov nakon
uklanjanja okolnih razmaka mora imati 1–200 znakova. Neispravan unos daje
`400`, nedostajući ili nepoznat identitet `401`, tuđi dokument ili nelokalna
veza `403`, a nepoznat identifikator `404`.

```powershell
$headers = @{ 'X-Test-User' = 'alice' }
$baseUri = 'http://127.0.0.1:5081'
$document = Invoke-RestMethod -Method Post -Uri "$baseUri/documents" -Headers $headers `
  -ContentType 'application/json' -Body (@{ title = 'Novi dokument' } | ConvertTo-Json)
$uri = "$baseUri/documents/$($document.id)"
Invoke-RestMethod -Method Get -Uri $uri -Headers $headers
Invoke-RestMethod -Method Put -Uri $uri -Headers $headers `
  -ContentType 'application/json' -Body (@{ title = 'Izmijenjen naslov' } | ConvertTo-Json)
Invoke-RestMethod -Method Delete -Uri $uri -Headers $headers
```

CRUD dokumenti postoje u memoriji do brisanja ili ponovnog pokretanja API-ja.
Rute Read demonstracije koriste zasebne početne dokumente `doc-1` i `doc-2`.

### Autorizovana sekvenca dokumenata

Uz pokrenut lokalni API i postavljen `ALICE_TEST_USER=alice`:

```powershell
dotnet run --project src/BolaDetector.Demo -- --document-sequence configs/demo-detection.json
```

Demo prikazuje JSON rezultate kreiranja, čitanja, provjere stanja, izmjene,
brisanja i potvrde odsustva privremenog dokumenta. Izlazni kodovi su `0` za
uspjeh, `1` za grešku i `130` za otkazivanje.
[Model, pravila izvršavanja i testovi](docs/implementation-v4-multi-step-sequences.md).

JSON sadrži i `Lifecycle` sa očekivanim i opaženim vlasništvom, istorijom
koraka, potvrdom brisanja i preostalim obavezama čišćenja.
[Model životnog ciklusa i ograničenja](docs/implementation-v5-1-resource-lifecycle-modeling.md).

Lokalni API podržava oporavak poslije izgubljenog Create odgovora putem
ključa ograničenog na vlasnika i namjeru kreiranja.
[Pouzdano kreiranje i čišćenje](docs/implementation-v5-2-reliable-creation-and-cleanup.md).

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
  BolaDetector.EndToEndTests/  # procesni NUnit testovi
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

JSON adapter priprema dokaze iz već prikupljenih snimaka, a domenski
analizator klasifikuje ishod. [Priprema dokaza i migracija ugovora](docs/architecture-audit.md#priprema-dokaza-i-domenska-klasifikacija).

.NET rješenje se kompajlira i prolazi **80 jediničnih i 24 integraciona
testova**. Integracioni testovi koriste stvarni ASP.NET pipeline preko
TestServer-a i pokrivaju CRUD, vlasništvo, validaciju, nelokalne veze i
postojeće ponašanje Read demonstracije.

Procesni E2E testovi pokrivaju autorizovani CRUD, autorizovani Read CLI i
CLI grešku konfiguracije. Za Read CLI test treba slobodan loopback port
`5081`. [Preduslovi, komande i čišćenje](tests/BolaDetector.EndToEndTests/README.md).

[Arhitektonska revizija i dokazna matrica](docs/architecture-audit.md)
navode granice validirane konfiguracije, preostala odstupanja i rezultate
završne provjere.

Detektor podržava čitanje u jednom koraku. CRUD operacije pripadaju lokalnom
testnom API-ju. OpenAPI importer je sačuvan, ali demo koristi eksplicitne
operacije iz JSON-a. YAML fajlovi su primjeri budućeg ugovora bez loadera.
Testni header predstavlja sintetički identitet lokalne aplikacije.

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
