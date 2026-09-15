# Mode operatoire Samba AD Core

Ce guide couvre la configuration d'un serveur HardenBox installe avec le profil `sambaad-core`, depuis la console locale jusqu'a la promotion Samba AD.

## Informations a preparer

Remplacez les exemples suivants par les valeurs de votre infrastructure :

| Parametre | Exemple |
|---|---|
| Interface reseau | `ens18` |
| Adresse du serveur | `192.168.10.10/24` |
| Passerelle | `192.168.10.1` |
| DNS initial | `1.1.1.1` |
| Nom du serveur | `dc01.ad.exemple.fr` |
| Realm Active Directory | `AD.EXEMPLE.FR` |
| Domaine NetBIOS | `EXEMPLE` |
| Reseau autorise | `192.168.10.0/24` |

Conservez un acces a la console de la machine. L'application de la nouvelle adresse IP coupe une eventuelle connexion SSH existante.

## 1. Configurer le socle Common

Connectez-vous localement avec le compte `hardenbox`, puis lancez :

```bash
sudo hb-common-config
```

Si la commande affiche `hardenbox n'est pas dans le fichier sudoers`, passez temporairement en root depuis la console locale :

```bash
su -
usermod -aG sudo hardenbox
exit
```

Fermez ensuite completement la session `hardenbox`, reconnectez-vous, puis verifiez et relancez l'assistant :

```bash
id
sudo hb-common-config
```

La sortie de `id` doit contenir le groupe `sudo`. Cette recuperation ne doit etre necessaire que sur une image anterieure au correctif d'installation.

Renseignez successivement :

1. le nom d'hote complet, par exemple `dc01.ad.exemple.fr` ;
2. le compte administrateur, normalement `hardenbox` ;
3. le port SSH, normalement `22` ;
4. le reseau autorise pour SSH, par exemple `192.168.10.0/24` ;
5. l'interface reseau, par exemple `ens18` ;
6. l'adresse IPv4 avec son prefixe, par exemple `192.168.10.10/24` ;
7. la passerelle, par exemple `192.168.10.1` ;
8. les DNS separes par des virgules, par exemple `1.1.1.1,9.9.9.9`.

Confirmez avec `o`. Le reseau statique est applique avant les operations APT, puis le durcissement Common est execute.

Suivez l'operation depuis la console :

```bash
sudo journalctl -fu hb-common-bootstrap.service
```

Le marqueur suivant doit exister apres la reussite :

```bash
sudo cat /var/lib/hardenbox/hb-common.completed
```

## 2. Verifier le reseau

```bash
ip -br address
ip route
ping -c 3 192.168.10.1
getent hosts debian.org
```

Verifiez aussi SSH depuis un autre poste :

```bash
ssh hardenbox@192.168.10.10
```

Adaptez l'adresse et le port si vous avez choisi d'autres valeurs.

## 3. Demarrer l'assistant Samba AD

Le service Web doit demarrer apres la reussite de Common :

```bash
sudo systemctl start hb-sambaad-setup-web.service
sudo systemctl status hb-sambaad-setup-web.service
```

Recuperez le jeton d'acces :

```bash
sudo cat /var/lib/hardenbox/sambaad-web.token
```

Le service ecoute uniquement sur `127.0.0.1:9090`. Il n'est pas expose directement sur le reseau.

## 4. Ouvrir le tunnel SSH

Depuis le poste d'administration, gardez cette commande ouverte :

```bash
ssh -L 9090:127.0.0.1:9090 hardenbox@192.168.10.10
```

Ouvrez ensuite dans un navigateur :

```text
http://127.0.0.1:9090
```

Saisissez le jeton obtenu a l'etape precedente.

## 5. Promouvoir le serveur

Pour creer une nouvelle foret, renseignez notamment :

- operation : `Creer une nouvelle foret` ;
- domaine DNS : `AD.EXEMPLE.FR` ;
- domaine NetBIOS : `EXEMPLE` ;
- controleur : `dc01` ;
- interface : `ens18` ;
- adresse : `192.168.10.10/24` ;
- passerelle : `192.168.10.1` ;
- redirecteur DNS : `1.1.1.1` ;
- reseau autorise : `192.168.10.0/24` ;
- niveau fonctionnel : `2016` ;
- mot de passe `Administrator` : au moins 14 caracteres avec majuscule, minuscule, chiffre et symbole.

Selectionnez les options de durcissement souhaitees, verifiez le resume, puis appliquez la configuration.

Suivez le provisionnement :

```bash
sudo journalctl -fu hb-sambaad-setup-web.service
```

Ne fermez pas la console et ne redemarrez pas le serveur pendant le provisionnement.

## 6. Option Exchange 2019

Les fichiers LDF Microsoft ne sont pas inclus dans HardenBox. Copiez le dossier `Setup/Data` du media Exchange 2019 sur le serveur, par exemple :

```text
/etc/Exchange/data
```

Le dossier doit contenir des fichiers tels que :

```text
PostWindows2003_schema84.ldf
PostWindows2003_schema85.ldf
schemaversion.ldf
```

Verifiez leur presence :

```bash
find /etc/Exchange/data -maxdepth 1 -type f -iname 'PostWindows2003_schema*.ldf' | head
```

Pendant une premiere promotion, choisissez `Etendre le schema apres la promotion` et indiquez le chemin complet du dossier LDF.

L'extension du schema est irreversible hors restauration complete de l'annuaire. L'outil verifie le role Schema Master et sauvegarde l'annuaire dans `/var/backups`.

## 7. Verifications apres promotion

```bash
sudo systemctl status samba-ad-dc.service
sudo systemctl status samba-adws.service
sudo samba-tool domain level show
sudo samba-tool fsmo show
host -t A dc01.ad.exemple.fr 127.0.0.1
host -t SRV _ldap._tcp.ad.exemple.fr 127.0.0.1
host -t SRV _kerberos._udp.ad.exemple.fr 127.0.0.1
sudo testparm --suppress-prompt
```

Le marqueur de reussite doit exister :

```bash
sudo ls -l /var/lib/hardenbox/hb-sambaad.completed
```

Redemarrez ensuite :

```bash
sudo systemctl reboot
```

## 8. Maintenance ulterieure

Pour rouvrir l'assistant Web sur un domaine provisionne :

```bash
sudo systemctl start hb-sambaad-setup-web.service
sudo cat /var/lib/hardenbox/sambaad-web.token
```

Recreez le tunnel SSH, puis choisissez l'une des operations :

- `Corriger le durcissement et les GPO` : applique la correction puis execute `samba-dcgpofix` ;
- `Etendre uniquement le schema Exchange` : applique uniquement les LDF Exchange, sans rejouer le durcissement ni la correction GPO.

Verifier les GPO sans modification :

```bash
sudo samba-dcgpofix --check
```

Les sauvegardes GPO sont conservees dans `/var/backups/samba-dcgpofix`.

## 9. Diagnostic

Common :

```bash
sudo systemctl status hb-common-bootstrap.service
sudo journalctl -u hb-common-bootstrap.service --no-pager -l
cat /var/lib/hardenbox/hb-common.result
```

Samba AD :

```bash
sudo systemctl status hb-sambaad-setup-web.service
sudo journalctl -u hb-sambaad-setup-web.service --no-pager -l
cat /var/lib/hardenbox/hb-sambaad.result
```

Si l'assistant Web ne repond pas, verifiez localement :

```bash
sudo ss -ltnp | grep ':9090'
sudo systemctl restart hb-sambaad-setup-web.service
```