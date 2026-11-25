# Analiza dokaza o izmjeni

`FindingAnalyzer.AnalyzeRecordedWrite` prima očekivanu dozvolu, HTTP status,
identifikator resursa, početni i završni snimak, naziv zaštićenog polja i očekivanu
JSON vrijednost. Pozivalac dostavlja snimke i potvrđuje da pripadaju resursu.
Metoda se nalazi u domenskom sloju i radi bez HTTP klijenta.

Potvrda izmjene zahtijeva da se očekivana vrijednost razlikuje od početne i
semantički podudara sa završnom. Ako model zabranjuje operaciju, dokaz takve
izmjene daje `ConfirmedViolation` čak i kada je status 403. Uspješan status
bez dokaza izmjene daje `PotentialViolation` za zabranjenu operaciju sa
pouzdanim snimcima. Nedostupan, neispravan ili nepotpun snimak daje
`LimitedVerification`. Istu klasifikaciju dobija dozvoljena operacija čiji
očekivani ishod nije potvrđen. Dokazi u rezultatu opisuju poređenje bez
uključivanja vrijednosti polja.

Promjene su u `FindingAnalyzer.cs`, `StateChangeAnalyzerTests.cs`, README-u
i ovom dokumentu. Analizator ima jedinične testove sa sintetičkim snimcima i
nema vezu sa izvršavanjem zahtjeva ili izvještavanjem iz demonstracije. Postojeće
PowerShell komande za Read demonstraciju nalaze se u README-u.

Kompajliranje rješenja pomoću lokalno keširanih paketa
(`dotnet build BolaDetector.sln -m:1 -p:NuGetAudit=false --ignore-failed-sources`)
završilo je bez grešaka i upozorenja. Prošlo je 15 ciljanih testova iz klasa
`StateChangeAnalyzerTests` i `ReadFindingAnalyzerTests`. Pokretanje testova
cijelog rješenja prekinuto je zbog zaključane datoteke tokom paralelnog
kompajliranja. Projekat `BolaDetector.EndToEndTests` nema otkrivenih testova.
Read demonstracija nije pokrenuta tokom ove provjere.
