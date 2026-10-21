# Schema Exchange 2019

Copiez ici le contenu du dossier `Setup/Data` du media Exchange 2019 avant de construire l'ISO.
Les fichiers indispensables sont `PostWindows2003_schema*.ldf` et `schemaversion.ldf`.

Ce dossier est installe par le paquet `hb-sambaad` dans `/usr/share/hardenbox/exchange-data`.
`exchange-schema-upgrade` et les assistants SambaADPromo et Web l'utilisent comme source :
il suffit de cocher l'extension du schema Exchange.

Les fichiers LDF sont fournis par Microsoft : verifiez que votre licence Exchange autorise
leur redistribution avant de publier l'ISO ou de les pousser dans un depot public.
