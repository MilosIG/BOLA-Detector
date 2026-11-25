# Pravila razvoja

- Poštuj [arhitektonska pravila](BolaDetector/docs/architecture-rules.md). Domain je nezavisan. Application koristi Domain i vlastite portove. Engine implementira aplikacione politike, a Infrastructure tehničke adaptere.
- Registruj konkretne implementacije na mjestu sastavljanja aplikacije. Unutrašnji slojevi koriste konstruktorsko ubrizgavanje potrebnih ugovora.
- Održavaj fokusirane odgovornosti, čitljiva imena i eksplicitne domenske invarijante. Uvedi novi tip, interfejs ili obrazac kada rješava konkretan problem.
- Očuvaj javne ugovore, lokalna ograničenja izvršavanja, sintetičke identitete i izolaciju `BolaTestApi`. Kredencijali i sirovi odgovori ne pripadaju izvještajima ili repozitoriju.
- Provjeri build i relevantne NUnit testove prije završetka. Za promjene granica pokreni arhitektonske testove. Za promjene postojećeg toka provjeri odgovarajuću Read ili CRUD regresiju. Evidentiraj stvarne rezultate i ograničenja.
- Dokumentuj značajne arhitektonske odluke, njihov razlog, rizik i uticaj na ugovore. Čuvaj postojeće korisničke izmjene.
- Pokreći semantičku validaciju prije generatora i razdvoji konfiguracione greške od nalaza. Konfiguraciju učitaj kao ulaz, a kroz portove jedne sesije proslijedi isti nepromjenljiv snimak. Ulaznu konfiguraciju i snimak sesije drži izvan DI registracije. Granice provjeri testovima ponašanja i stvarnih referenci.
- Parsiraj format dokaza u Infrastructure. Application prosljeđuje pripremljene dokaze, a Domain klasifikuje njihove ishode bez JSON tipova.
