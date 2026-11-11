# Bilgisayarsız build: Unity lisansı kurulumu

Workflow: Actions > "Unity iOS build + TestFlight (bilgisayarsiz)" > Run workflow.

GitHub'da Unity'nin açılması için ücretsiz (Personal) lisansı bir kere aktive edip GitHub'a 3 gizli anahtar olarak eklemek gerekir.

Settings > Secrets and variables > Actions > New repository secret:
- `UNITY_EMAIL`    Unity hesabının e-postası
- `UNITY_PASSWORD` Unity hesabının şifresi
- `UNITY_LICENSE`  Aşağıdaki adımlarla alınan .ulf dosyasının TÜM içeriği

## UNITY_LICENSE nasıl alınır
1. Actions sekmesinden "Unity lisans dosyası iste (bir kere)" workflow'unu çalıştır.
2. Bittiğinde sayfanın altındaki Artifacts'ten `Unity_v6000.3.27f1.alf` dosyasını indir.
3. https://license.unity3d.com/manual adresinde bu .alf dosyasını yükle, Personal'ı seç, `.ulf` dosyasını indir.
4. .ulf dosyasını metin olarak aç, tüm yazıyı kopyalayıp `UNITY_LICENSE` secret'ına yapıştır.
