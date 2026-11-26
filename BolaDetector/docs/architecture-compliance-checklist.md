# Matrica arhitektonske usklađenosti

Oznake P1–P13 upućuju redom na `1.pdf` do `13.pdf`. Svih 13
numerisanih prezentacija nedostaje. Za svaki red broj stranice i direktna
provjera prema prezentaciji imaju status **NOT VERIFIED**. Tabela ocjenjuje
kriterijume iz zadatka. Stavke o konzistentnosti i nepromjenljivosti
prikazuju stanje neposredno prije i poslije uvođenja snimka sesije.
Ostale stavke prikazuju stanje iz revizije. Za P11–P13 raspored grupa slijedi
redoslijed iz zadatka.

| Referenca | Kriterijum | Prije | Poslije | Dokaz i test | Obrazloženje |
| --- | --- | --- | --- | --- | --- |
| P1 | Inkrementi i provjerljivi kriterijumi | PASS | PASS | `git log`, `implementation-v3-configuration-validation.md`, `architecture-rules.md` | Read i CRUD su u istoriji. Validacija ima kriterijume i rezultate u radnom stablu. |
| P2 | Izvor, lokalne datoteke i primjeri | PASS | PASS | `.gitignore`, `.env.example`, `git ls-files` | Bin, obj i lokalni .env nisu praćeni. |
| P2 | Generisani i arhivski sadržaj | PARTIAL | PARTIAL | `BolaDetector.zip`, `bola-report.json`, `reports/demo-report.json` | Demo primjer je dokumentovan. Namjena dodatnog izvještaja i arhiva čeka potvrdu. |
| P2 | Tajne kroz punu istoriju | NOT VERIFIED | NOT VERIFIED | Pregled aktivnih konfiguracija | Lokalni primjeri i `env:` reference su pregledani. Nije izvršena potpuna pretraga istorije i arhiva. |
| P3 | Agilno planiranje i Definition of Done | PASS | PASS | `architecture-audit.md`, `architecture-rules.md` | Prioriteti i provjerljiv završetak dokumentovani za individualni rad. |
| P4 | Imenovanje, veličina i odgovornosti | PARTIAL | PARTIAL | `DocumentCrudEndpoints`, `FindingAnalyzer`, `App.tsx`; CRUD testovi | Izdvojen filter pristupa. Ostaju dva različita ugovora analize i problem prikaza dijakritike. |
| P4 | Null i validacija pri ulazu | PASS | PASS | `ConfigurationValidatorTests`, `ConfigurationFlowTests`, CRUD validacija | Početna validacija odbija null i nekonzistentne konfiguracije. Loader sada eksplicitno obrađuje izostali rezultat deserijalizacije. |
| P4 | Dijagnostika bez osjetljivih vrijednosti | PASS | PASS | `JsonDetectionConfigurationLoader.SafePath`, `ConfigurationFlowTests` | Dodat slučaj pogrešnog JSON tipa za ugniježđenu referencu kredencijala uz tačnu putanju. |
| P4 | Obrada grešaka, resursi i otkazivanje | PARTIAL | PARTIAL | `HttpScenarioRequestExecutor.SendAsync`; `DetectionExecutionTests` | Timeout i otkazivanje su testirani. Široki catch može pogrešno pripisati grešku kredencijalima. |
| P4 | Formatiranje i analizatori | PASS | PASS | `Directory.Build.props`, `dotnet format --verify-no-changes`, Release build | SDK analiza i nullable su aktivni. Nema novih suppressions. |
| P4 | Performanse | PARTIAL | PARTIAL | `BolaScenarioGenerator.Generate`, `JsonReadEvidenceVerifier.Project` | Dodatne pretrage i sortiranja su evidentirani. Njihov uticaj nije mjeren. |
| P5 | Apstrakcija, polimorfizam i kompozicija | PASS | PASS | `DetectionEngine`, `RunDetection`; `DetectionPortTests` | Adapteri se mijenjaju kroz portove, a saradnici se komponuju konstruktorima. |
| P5 | Enkapsulacija i jednakost | PARTIAL | PARTIAL | `ApiProject`, `ValidatedDetectionConfigurationTests`, `TestDocumentStoreTests` | Store i snimak čuvaju svoje kolekcije. Pojedina svojstva API projekta su promjenljiva. |
| P5 | Nepromjenljivost konfiguracije u sesiji | FAIL | PASS | `ValidatedDetectionConfiguration.Create`, `ValidatedDetectionConfigurationTests` | Kopirani podaci se validiraju i zamrzavaju u nepromjenljive liste sa zapečaćenim record elementima. |
| P5 | Opravdano nasljeđivanje | PASS | PASS | `BolaDbContext`, testni `WebApplicationFactory` | Hijerarhije su plitke i vezane za postojeće framework ugovore. |
| P6 | SRP za analizu dokaza | PARTIAL | PASS | `JsonRecordedWriteEvidencePreparer`, `AnalyzeRecordedWrite`, `FindingAnalyzer` | Adapter priprema poređenje, Application orkestrira, Domain klasifikuje. |
| P6 | OCP | PASS | PASS | Portovi adaptera, `DetectionPortTests` | Zamjena adaptera ne zahtijeva izmjenu centralne politike. |
| P6 | LSP | PARTIAL | PARTIAL | `IDetectionEngine`, `TestExecution`, `RunDetection`; port testovi | Jasniji preduslovi i postuslovi. Tip i dalje dopušta izostavljen rezultat. |
| P6 | ISP | PASS | PASS | `IReadEvidenceVerifier`, `IApiProjectReader`, `IDetectionReportWriter` | Ugovori daju klijentu potrebnu operaciju bez suvišnih metoda. |
| P6 | DIP i životni vijekovi | PASS | PASS | DI ekstenzije, Demo composition root, portovi | Odluke koriste ugovore. Scoped čitač prati EF kontekst. |
| P7 | Smjer referenci i ciklusi | PASS | PASS | `.csproj`, `DependencyRulesTests`, Mermaid graf | Ulazni i infrastrukturni projekti referenciraju unutrašnje slojeve. Direktne Demo reference sada su eksplicitne. |
| P7 | Izolacija testnog API-ja | PASS | PASS | `BolaTestApi.csproj`, projektni arhitektonski test | Produkcioni detektor i ciljni API nemaju međusobne projektne reference. |
| P7 | Izolacija HTTP klijenta i frameworka | PASS | PASS | `DependencyRulesTests` i projektne reference | Nema ASP.NET, EF, DI, OpenAPI ni HTTP klijenta u jezgru. Binarni test sada eksplicitno štiti HTTP granicu. |
| P7 | Application i Engine bez JSON serijalizacije | PASS | PASS | `InnerAssemblies_CompiledReferences_ExcludeJsonSerialization` | Binarna provjera štiti postojeću granicu. |
| P7 | Domain bez JSON formata i parsiranja | FAIL | PASS | `FindingAnalyzer`, `RecordedWriteEvidence`, `InnerAssemblies_CompiledReferences_ExcludeJsonSerialization` | Domenski sklop nema referencu na System.Text.Json. Snimak pripada Application ulazu, a parser infrastrukturi. |
| P7 | Jedna konzistentna konfiguracija po sesiji | PARTIAL | PASS | `RunDetection`, `DetectionPortTests`, `ConfigurationFlowTests` | DI nema konfiguracioni parametar. Svi portovi koriste istu instancu snimka, a dvije preklopljene sesije zadržavaju vlastita polja dokaza. |
| P7 | Komande i upiti | PASS | PASS | `EfApiProjectReader`, `TestDocumentStore`; CRUD regresija | Upit čita stanje. Komanda kreiranja vraća ID kroz kreirani dokument. |
| P8 | Piramida i stvarna integracija | PARTIAL | PARTIAL | NUnit, `DocumentCrudTests`, `DetectionIntegrationTests`, `LocalRegressionTests` | Procesni testovi su implementirani. Rezultati i preduslov slobodnog CLI porta su dokumentovani u arhitektonskoj reviziji. |
| P8 | OpenAPI parser i dijagnostika | PASS | PASS | `OpenApiImporterTests` | Izolovani testovi provjeravaju mapiranje i dijagnostiku. Integracija importa s detekcijom nije predmet ovog kriterijuma. |
| P8 | Izvještaj, CRUD i Read regresija | PASS | PASS | `JsonReportTests`, `DocumentCrudTests`, `DetectionIntegrationTests` | Integracija potvrđuje četiri operacije, odbijanje tuđeg pristupa i očekivane Read ishode. |
| P8 | Mjereno pokrivanje | NOT VERIFIED | NOT VERIFIED | Vidi rezultate verifikacije u reviziji | Statusi testova sami ne potvrđuju procenat pokrivenosti. |
| P9 | Testabilnost i testni dvojnici | PASS | PASS | `DetectionPortTests`, `DetectionExecutionTests` | Postoje fiksni sat i stubovi, uz memorijski transport koji zamjenjuje mrežu. |
| P9 | Imenovanje i Arrange–Act–Assert | PARTIAL | PARTIAL | `OpenApiImporterTests`, postojeći testovi | Novi testovi imaju trodijelna imena. Raniji nazivi koriste mješovite stilove. |
| P9 | Odvajanje upita radi testabilnosti | PASS | PASS | `IApiProjectReader`, `EfApiProjectReader`, `FindCreated` | Port skriva EF, a memorijski upit je provjerljiv bez servera. |
| P10 | Poddomeni, ograničeni konteksti i jezik | PASS | PASS | `domain-glossary.md`, `AccessRule`, izolovani `BolaTestApi` | Rječnik sada obuhvata i rezultate validacije, uz jasnu granicu prema nalazima. |
| P10 | Entiteti, vrijednosti i invarijante | PARTIAL | PARTIAL | `TestIdentity`, `TestResource`, `ApiProject`, `TestScenario` | Koncepti su odvojeni. Neki modeli dopuštaju nepotpuno ili promjenljivo stanje. |
| P10 | Domenski servisi i perzistencija | PASS | PASS | `AccessRule.Allows`, `AuthorizationOracle`, `IApiProjectReader` | Politika je odvojena od načina čitanja podataka. |
| P10 | Agregati i domenski događaji | N/A | N/A | `TestDocumentStore`, `DetectionReport` | Nema utvrđene potrebe za zajedničkom domenskom konzistentnošću više objekata ili za nezavisnim pretplatnicima. |
| P11 | DI Singleton, Factory Method, Builder i Prototype | PASS | PASS | DI ekstenzije, `WebApplication.CreateBuilder`, `TestDocumentStore.Update` | Framework mehanizmi i kopiranje record vrijednosti odgovaraju stvarnim potrebama. GoF globalni Singleton nije prisutan. |
| P11 | Abstract Factory | N/A | N/A | DI ekstenzije | Nema porodica proizvoda s međuzavisnim konstrukcijama. |
| P12 | Adapter i Facade | PASS | PASS | `EfApiProjectReader`, `JsonDetectionReportWriter`, `RunDetection` | Tehničke granice su adaptirane, a use case izlaže jednostavan ulaz. |
| P12 | Bridge, Decorator i Composite | N/A | N/A | Portovi, HTTP DI, `TestScenario.Steps` | Nezavisne hijerarhije, aplikacioni dekoratori i ugniježđeno stablo nisu potrebni sadašnjem toku. |
| P13 | Strategy, Chain of Responsibility, Iterator i Observer | PASS | PASS | `IReadEvidenceVerifier`, endpoint filter, LINQ, `Console.CancelKeyPress` | Zamjenljivo ponašanje i standardni framework mehanizmi koriste se na odgovarajućim granicama. |
| P13 | Template Method, Command, Memento, State i Visitor | N/A | N/A | `RunDetection`, `ResourceSnapshot`, `ExecutionStatus` | Modeli podataka i CRUD operacije nemaju saradnje potrebne za ove obrasce. Kompozicija pokriva trenutnu složenost. |

Sekvence dokumenata primjenjuju kriterijume P7 kroz aplikacione portove,
nepromjenljiv snimak konfiguracije i izolovan kontekst izvršavanja. Provjere
obuhvataju binarne reference, validaciju prije transporta i integraciju s
lokalnim testnim API-jem. [Dokumentacija sekvenci](implementation-v4-multi-step-sequences.md).
Registar životnog ciklusa proširuje iste provjere izolacijom po izvršavanju,
nepromjenljivim rezultatima i evidencijom potvrđenih i neizvjesnih ishoda.
[Dokumentacija životnog ciklusa](implementation-v5-1-resource-lifecycle-modeling.md).
