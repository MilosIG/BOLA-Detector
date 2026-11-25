# Centralizovana validacija konfiguracije

Ovaj dokument opisuje treći inkrement. Aktuelni ugovori i nepromjenljivi
snimak sesije opisani su u [arhitektonskoj reviziji](architecture-audit.md#nepromjenljiv-snimak-konfiguracije-po-sesiji).

## Problem i tok izvršavanja

JSON deserijalizacija mogla je postaviti kolekcije na `null` i pored
inicijalizatora u modelu. Generator je zatim pretpostavljao da kolekcije
postoje, a neke provjere odredišta, timeouta i zaštićenih polja izvršavale su
se tek u adapterima. Greška u kasnijoj operaciji zato je mogla prekinuti
izvršavanje nakon prethodnih zahtjeva.

1. Infrastructure `JsonDetectionConfigurationLoader` provjerava fajl,
   sintaksu i korijenski objekat, prisustvo obaveznih kolekcija i JSON tipove.
2. Application `RunDetection` poziva `ConfigurationValidator` prije
   generatora i izvršioca. Validator radi i nad konfiguracijama sastavljenim
   u kodu. Ako pozivalac koristi drugu instancu od one registrovane za
   adaptere, provjeravaju se obje.
3. `ValidatedDetectionConfiguration` prenosi rezultat provjere od
   `RunDetection` do postojećeg generatora. Direktan poziv
   `BolaScenarioGenerator.Generate` koristi istu validaciju i zadržava
   `ArgumentException` uz strukturirane greške kao unutrašnji izuzetak.
4. `DetectionInputPolicy` dijeli provjere odredišta, rute, ID-a, indikatora
   kredencijala i zaštićenih polja s relevantnim adapterima. HTTP adapter
   i dalje provjerava lokalnu adresu i konačni URI.
5. Demo ispisuje strukturirane greške i vraća kod `1`. Izvještaj se piše
   tek nakon uspješnog povratka iz `RunDetection`.

Domenska odluka o vlasničkom čitanju ostaje u `AccessRule.Allows`.
Generator i dalje koristi `IAuthorizationOracle` za odluke o scenarijima.
Validacija ne čita varijable okruženja niti kontaktira testni API.

## Datoteke

| Sloj | Dodato ili izmijenjeno |
| --- | --- |
| Application | Dodati `ConfigurationValidator.cs`, `ConfigurationValidationError.cs`, `ConfigurationValidationException.cs`, `ValidatedDetectionConfiguration.cs` i `DetectionInputPolicy.cs`. Izmijenjeni `DetectionConfiguration.cs`, `RunDetection.cs` i `IScenarioGenerator.cs`. |
| Engine | Izmijenjen `BolaScenarioGenerator.cs` radi korišćenja jedinstvene provjere na direktnom ulazu. |
| Infrastructure | Izmijenjeni `JsonDetectionConfigurationLoader.cs`, `LocalTargetAddressPolicy.cs`, `EnvironmentTestCredentialProvider.cs` i `JsonReadEvidenceVerifier.cs`. |
| Demo | Izmijenjen `Program.cs` radi prikaza validacionih grešaka. |
| Testovi | Dodati `ConfigurationValidatorTests.cs` i `ConfigurationFlowTests.cs`. `DetectionIntegrationTests.cs` pokreće Read regresiju kroz `RunDetection`. |
| Dokumentacija | Ažurirani `README.md`, `architecture-audit.md` i `architecture-compliance-checklist.md`. |

## Pravila i kompatibilnost

- Obavezne JSON kolekcije su `identities`, `resources`, `operations`
  i `rules`. Izostavljanje, eksplicitni `null`, prazna kolekcija i
  `null` element imaju odgovarajuću dijagnostiku.
- `targetOrigin`, `timeoutSeconds` i `protectedProperties` imaju
  postojeće podrazumijevane vrijednosti kad su izostavljeni. Eksplicitno
  nevaljana vrijednost odbija se. Odredište ostaje `http://127.0.0.1:5081`,
  timeout je 1–60 sekundi.
- ID-evi identiteta i operacija jedinstveni su u konfiguraciji, a ID
  resursa unutar tipa resursa. Obavezni tekstualni atributi moraju sadržati
  tekst. Referenca na kredencijal koristi `env:NAZIV`; dostupnost varijable
  provjerava se tek pri korišćenju. Prikazni nazivi identiteta ne služe kao ID.
- Model koristi organizacione GUID-ove bez zasebnog registra. Identifikatori
  moraju biti neprazni, a vlasnik mora referencirati postojeći identitet.
  Različite organizacije vlasnika i resursa blokiraju referentno čitanje
  kada primjenjivo pravilo zahtijeva istu organizaciju.
- Trenutno su podržani `GET` i `read`. Ruta mora imati jedan `{id}`
  bez drugih parametara, queryja ili fragmenta. Operacija mora imati
  odgovarajući tip resursa i tačno jedno pravilo za tip i akciju.
- Polja dokaza moraju biti jedinstvena, neprazna i različita od `id`.
  Istoimene ključeve JSON svojstava loader i dalje prihvata kao ranije;
  JSON imena se mapiraju bez obzira na velika i mala slova. Konfiguracija
  nema enum svojstva. Nepodržane akcije i metode provjeravaju se kao
  tekst. Izostavljeni logički indikatori pravila zadržavaju CLR vrijednost
  `false`.

Validan potpuni primjer je [demo-detection.json](../configs/demo-detection.json).
Isti fajl ostaje validan bez svojstava `targetOrigin`, `timeoutSeconds`
i `protectedProperties`. Promjena `"identities": null` daje
`CFG_REQUIRED_COLLECTION` na `identities`. Promjena
`"protectedProperties": ["title", "id"]` daje `CFG_INVALID_VALUE`
na `protectedProperties[1]`.

## Kodovi grešaka

| Kod | Uzrok i primjer putanje |
| --- | --- |
| `CFG_EMPTY_FILE` | Prazan fajl, `$` |
| `CFG_INVALID_JSON` | Sintaksna greška, `$` |
| `CFG_INVALID_TYPE` | Pogrešan JSON tip ili format vrijednosti, `timeoutSeconds` |
| `CFG_REQUIRED_COLLECTION` | Nedostaje kolekcija, vrijednost je null ili je kolekcija prazna, `identities` |
| `CFG_REQUIRED` | Nedostaje objekat, tekst ili ID, `identities[0].identity.id` |
| `CFG_DUPLICATE_ID` | Ponovljen identifikator, `operations[1].operation.operationId` |
| `CFG_INVALID_REFERENCE` | Nepoznat vlasnik ili tip, `resources[0].ownerId` |
| `CFG_INVALID_CREDENTIAL_REFERENCE` | Nepodržan format reference, `identities[0].identity.credentialReference` |
| `CFG_INVALID_ORIGIN` | Odredište van lokalne politike, `targetOrigin` |
| `CFG_OUT_OF_RANGE` | Timeout izvan raspona, `timeoutSeconds` |
| `CFG_UNSUPPORTED_OPERATION` | Nepodržana metoda ili akcija, `operations[0].operation.method` |
| `CFG_INVALID_ROUTE` | Neispravan predložak putanje, `operations[0].operation.path` |
| `CFG_AMBIGUOUS_RULE` | Više pravila istog tipa i akcije, `rules[1]` |
| `CFG_MISSING_RULE` | Nedostaje pravilo za operaciju, `operations[0].action` |
| `CFG_OWNER_BASELINE_DENIED` | Pravilo odbija čitanje vlasniku, `resources[0].ownerId` |
| `CFG_INVALID_VALUE` | Nedozvoljen ID ili zaštićeno polje, `protectedProperties[1]` |
| `CFG_DUPLICATE_VALUE` | Ponovljeno zaštićeno polje, `protectedProperties[1]` |

Aplikacioni validator prikuplja nezavisne greške deterministički. Faza
deserijalizacije prijavljuje prvu sintaksnu ili konverzionu grešku; semantička
provjera slijedi nakon uspješne deserijalizacije. Putanje i poruke ne
uključuju sirove vrijednosti ili tekst izuzetka parsera.

## Provjere

| Provjera | Rezultat |
| --- | --- |
| Release build | Uspješan, bez grešaka i upozorenja |
| NUnit jedinični testovi | 61 prošao, od toga 49 postojećih i 12 novih |
| NUnit integracioni testovi | 21 prošao, od toga 9 postojećih i 12 novih |
| Arhitektonska provjera | Prošla, uz postojeći graf projektnih zavisnosti |
| `dotnet format --verify-no-changes --no-restore` | Prošao |
| Nevaljan Demo ulaz | `identities: null` daje `CFG_REQUIRED_COLLECTION`, izlazni kod 1 i nijedan generisan izvještaj |
| Read preko TestServer-a | 8 scenarija, 6 bez uočenog kršenja i 2 potvrđena nalaza |
| Autorizovani lokalni CRUD | POST 201, GET 200, PUT 204, DELETE 204, uz potvrđeni izmijenjeni naslov |
| Analiza snimaka izmjene | Postojeći jedinični testovi prolaze |

Integracioni testovi koriste brojače generatora i HTTP handlera. Na
neispravnom ulazu oba brojača ostaju na nuli. Za validan ulaz potvrđuju
poziv generatora i nastavak kroz postojeći HTTP adapter.

Kompajliranje koristi lokalno dostupne pakete, uz `NuGetAudit=false` pri
obnavljanju. Debug build u ovom inkrementu nije pokretan. Postojeći
`BolaTestApi` proces nije zaustavljan. Read regresija izvršena je kao
integracioni test; uspješna Read CLI demonstracija nije zasebno ponovljena.
Projekat `EndToEndTests` nema testove.

Programski model zadržava promjenjljive liste radi kompatibilnosti.
Pozivaoci treba da završe izmjene konfiguracije prije početka izvršavanja.
Prikladan naredni inkrement je dodati procesni E2E test za autorizovani
CRUD i provjeru izlaznog koda Demo-a pri neispravnom ulazu.
