# Domenski rječnik

Očekivana odluka potiče iz eksplicitnog modela. HTTP odgovor i provjereni
dokazi predstavljaju opaženo ponašanje. Klasifikator poredi te dvije strane.

| Termin | Značenje i odgovornost | Kontekst i povezani tipovi |
| --- | --- | --- |
| API projekat | Evidencija API-ja, njegove adrese i specifikacije | Platforma, `ApiProject`, `IApiProjectReader` |
| Operacija | Identifikator, HTTP metoda i predložak putanje | Detektor, `ApiOperation`, `ConfiguredOperation` |
| Testni identitet | Subjekt pristupa s organizacijom, ulogom i referencom na kredencijal | Detektor, `TestIdentity`, `ConfiguredIdentity`, `ITestCredentialProvider` |
| Nalog platforme | Identitet korisnika glavnog ASP.NET Core API-ja | Platforma, `IdentityUser` |
| Organizacija | Granica pripadnosti identiteta i resursa | Autorizacija, `Organization`, `TestIdentity.OrganizationId`, `TestResource.OrganizationId` |
| Resurs | Objekat ciljnog API-ja s ID-em, tipom i vlasnikom | Autorizacija, `TestResource` |
| Vlasništvo | Veza između ID-a resursa i identiteta njegovog vlasnika | Detektor, `TestResource.OwnerId`, `AccessRule`. U testnom API-ju vlasnika predstavlja `TestDocument.Owner`. |
| Pravilo pristupa | Uslovi pripadnosti tipu, vlasništva i organizacije za datu akciju | Autorizacija, `AccessRule`, `AuthorizationOracle`. Politika označava skup primjenjivih pravila. |
| Scenario | Kontekst očekivane odluke, identiteti, resurs i operacije | Detektor, `TestScenario` |
| Korak scenarija | Konfigurisana operacija u `TestScenario.Steps`. Izvršilac trenutno prihvata jedan GET korak. | Detektor, `ApiOperation`, `DetectionEngine` |
| Kontrolni odgovor | Odgovor dobijen identitetom vlasnika za provjeru dokaza | Detektor, `ObservedResponse`, `IReadEvidenceVerifier` |
| Snimak resursa | Zabilježena čitljivost, identifikator i sadržaj prije pripreme dokaza | Application, `ResourceSnapshot` |
| Poređenje snimaka | Ishod poređenja dva upotrebljiva snimka, jednaki, različiti ili bez pouzdanog poređenja | Domain, `SnapshotComparison` |
| Dokaz o snimljenoj izmjeni | Nepromijenjeno stanje, namjeravana izmjena, druga izmjena ili neupotrebljiv dokaz | Domain, `RecordedWriteEvidence` |
| Dokaz | Podatak potreban za opravdavanje klasifikacije, npr. podudaranje polja i otisak | Analiza, `ReadEvidence`, `FindingAnalysis` |
| Izvršavanje | Jedno pokretanje scenarija s ID-em, vremenom i ishodom | Aplikacija, `TestExecution`, `ExecutionStatus` |
| Klasifikacija | Ocjena rezultata provjere autorizacije | Analiza, `FindingVerdict`, `FindingAnalyzer` |
| Nalaz | Potvrđeno ili potencijalno kršenje povezano s izvršavanjem | Detektor, `VulnerabilityFinding` |
| Rezultat scenarija | Javni pregled očekivanja, opažanja, klasifikacije i dokaza | Izvještavanje, `ScenarioResult`, `DetectionReport` |
| Rezultat validacije konfiguracije | Prazna ili popunjena lista grešaka nakon provjere spremnosti konfiguracije | Application, `ConfigurationValidator.Validate`, `IReadOnlyList<ConfigurationValidationError>` |
| Greška konfiguracije | Stabilan kod, putanja i sigurna poruka o neispravnom ulazu | Ulazna granica, `ConfigurationValidationError`, `ConfigurationValidationException` |
| Ulazna konfiguracija | Deserijalizovani ili programski sastavljen DTO s listama koje pozivalac može uređivati između pokretanja | Application i Infrastructure, `DetectionConfiguration`, `JsonDetectionConfigurationLoader` |
| Validirani snimak konfiguracije | Nepromjenljiv skup parametara, identiteta, operacija, resursa, pravila i zaštićenih polja za jedno izvršavanje | Application, `ValidatedDetectionConfiguration.Create`, `RunDetection`, portovi za generisanje i izvršavanje |
| Artefakt reprodukcije | Predviđeni zapis sanitizovanih zahtjeva i preduslova | Model, `ReplayArtifact`. Trenutno nema izvršnog toka za ovaj tip. |
| Testni dokument | Sintetički objekat zasebnog lokalnog REST API-ja | Testni API, `TestDocument`, `TestDocumentStore` |
| Sekvenca dokumenata | Uređeni autorizovani koraci nad privremenim lokalnim dokumentima | Application, `DocumentSequence`, `DocumentStep` |
| Logički resurs sekvence | Ime koje Create povezuje s generisanim ID-em u jednom izvršavanju | Application i Engine, `DocumentStep.Resource`, `DocumentSequenceExecutor` |
| Ishod koraka sekvence | Uspjeh, greška, preskakanje ili otkazivanje | Application, `DocumentStepResult`, `SequenceStepOutcome` |
| Praćeni resurs | Logički resurs jednog izvršavanja sa potvrđenim stvarnim ID-em kada je poznat | Engine i Application, `DocumentResourceRegistry`, `TrackedDocumentResult` |
| Očekivano i opaženo vlasništvo | Identitet i organizacija iz konfiguracije, te zasebne vrijednosti i podudaranja iz opažanja | Application, `ExpectedDocumentOwnership`, `ObservedDocumentOwnership` |
| Obaveza čišćenja | Status uklanjanja privremenog resursa, uključujući neriješeno kreiranje bez poznatog ID-a | Application, `ResourceCleanupStatus` |
| Potvrđeno odsustvo | Očekivani 404 iz zasebnog VerifyAbsent koraka | Application, `DocumentLifecycleState.AbsenceVerified` |
| Korelacija kreiranja | Stabilan ključ namjere, vezan za vlasnika, organizaciju i jedno izvršavanje | `DocumentCreationIntent`, `DocumentResourceRegistry` |
