# HB-SAMBADWS

Ce paquet fournit le serveur Samba ADWS issu de `Packages/Source ADWS`.

Seules les arborescences Python d'execution `src/` et `classes/` sont installees dans `/usr/lib/hardenbox/samba-adws`. Le service `samba-adws.service` ecoute par defaut sur le port TCP 9389.

Le paquet est inclus dans l'ISO par le profil de construction `adws-payload`, mais aucun profil d'installation ne le selectionne. Pendant l'installation de `hb-sambaad`, le fichier Debian est copie depuis le DVD vers `/var/cache/hardenbox/packages/hb-sambadws.deb`. Le script de promotion l'installe seulement apres validation de Samba AD, DNS, Kerberos et SYSVOL.