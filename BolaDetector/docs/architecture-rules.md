# Arhitektonska pravila

## Zavisnosti i odgovornosti

| Projekat | Dozvoljene direktne projektne zavisnosti | Odgovornost |
| --- | --- | --- |
| Domain | Nema | Pojmovi, invarijante i pravila autorizacije i klasifikacije |
| Application | Domain | Slučajevi korišćenja, portovi i modeli razmjene |
| Engine | Application, Domain | Generisanje, orkestracija pojedinačnog scenarija i implementacija autorizacionog porta |
| Infrastructure | Application, Domain, Engine | HTTP, JSON, fajlovi, OpenAPI, kredencijali, EF i registracija implementacija |
| Api | Application, Engine, Infrastructure | ASP.NET Core granica, autentikacija i sastavljanje aplikacije |
| Demo | Application, Domain, Infrastructure | Konzolni ulaz, prikaz, izlazni kodovi i sastavljanje aplikacije |
| Web | HTTP ugovor platforme | React i TypeScript prezentacija |
| BolaTestApi | Nema | Sintetički identiteti, demonstracioni dokumenti i lokalni CRUD |

Testni projekti smiju referencirati komponente koje provjeravaju. Produkcioni
detektor komunicira s lokalnim testnim API-jem preko HTTP granice. Smjer
poziva u izvršavanju može prelaziti prema adapteru preko porta, dok referenca
izvornog koda ostaje usmjerena prema ugovoru.

## Smještanje, imenovanje i modelovanje

- Koristi engleska imena iz [domenskog rječnika](domain-glossary.md).
  Imenuj operaciju prema njenom efektu i razdvoji termin testa od termina nalaza.
- Pravilo autorizacije smjesti uz domenski model. Transportnu validaciju
  smjesti na ulaznu granicu, a poslovne invarijante provjeri tamo gdje nastaje
  ili se mijenja relevantno stanje.
- DTO koristi na granici kada njegov oblik ili životni ciklus odstupa od
  domena. Klasifikaciju zasnivaj na provjerenim dokazima. Odvoji sirove
  podatke od javnih rezultata.
- Uvedi vrednosni objekat kada može zaštititi invarijantu ili spriječiti zamjenu
  koncepata. Agregat uvedi ako pravila zahtijevaju zajedničku konzistentnost.
- Upit neka čita stanje. Komanda kreiranja može vratiti novi ID ili kreirani
  resurs kako bi pozivalac saznao ishod operacije.
- Učitavanje formata prepusti adapteru. Semantičku provjeru završi prije
  generisanja scenarija i zadrži konačne provjere u transportu. Domenska
  odluka pripada `AccessRule`. Strukturirane validacione poruke izostavljaju
  sirove vrijednosti.
- `RunDetection` formira i validira vlastiti snimak na početku svakog
  izvršavanja. Generator, Engine, HTTP adapter i verifikator primaju isti
  snimak eksplicitno kroz portove. Registracija servisa ne prima
  konfiguraciju. Završi pripremu ulaza prije poziva kako bi kopiranje
  predstavljalo namjeravano stanje. Ulaz se nakon kopiranja može koristiti
  za pripremu naredne sesije.
- Stvarna nepromjenljivost zahtijeva nepromjenljivu kolekciju i
  nepromjenljive elemente. Kada se proširuje model snimka, upotrijebi
  nepromjenljive vrednosne tipove ili kopiraj i zamrzni novo ugniježđeno stanje.
- JSON parsiranje i poređenje vrijednosti formata pripadaju adapteru.
  Domain klasifikuje pripremljene dokaze i koristi domenske ishode
  poređenja. Sirovi snimci ostaju na aplikacionoj ulaznoj granici.

## DI, testovi i obrasci

- Koristi konstruktorsko ubrizgavanje za saradnike. Razrješavanje servisa
  pripada composition root-u. Singleton zahtijeva bezbjednu konkurentnu
  upotrebu, a EF kontekst i njegov čitač imaju scoped životni vijek.
- Jedinični test treba da provjerava javno ponašanje i kontroliše spoljašnje
  zavisnosti. Koristi scenarijska imena poput
  `AnalyzeRecordedWrite_WhenSnapshotIsUnreadable_ReturnsLimitedVerification`.
- Integracioni test neka koristi stvarne implementacije granica koje ispituje.
  Razlikuj TestServer od testa procesa preko mreže. U testovima izvještavanja
  provjeri redakciju kredencijala i tijela odgovora.
- Procesni test mora provjeriti spremnost vlastitog procesa, ograničiti
  trajanje i očistiti svoje podatke. Sukob s postojećim procesom prijavi.
  Zavisnosti E2E projekta od aplikacija služe njihovom kompajliranju.
- Uvođenje obrasca opravdaj konkretnom promjenom ili nužnom izolacijom.
  Zabilježi posljedice za ugovor, DI i testove u tehničkoj dokumentaciji.

## Definition of Done

1. Implementacija pokriva provjerljive kriterijume prihvatanja.
2. Rješenje se kompajlira i relevantni testovi prolaze.
3. Provjerene su regresije postojećeg ponašanja i arhitektonske granice.
4. Dokumentacija odražava stvarno ponašanje, odluke i ograničenja provjere.
5. Diff je pregledan i spreman za ljudski pregled.
