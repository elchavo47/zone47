# Bilgisayarsız build: Unity hesabı kurulumu

Settings > Secrets and variables > Actions > New repository secret:
- `UNITY_USERNAME`  Unity ID e-postası
- `UNITY_PASSWORD`  Unity ID şifresi

Sonra Actions > "Unity iOS build + TestFlight (bilgisayarsiz)" > Run workflow.

Notlar: Unity ID'de iki adımlı doğrulama (2FA) açıksa bu yöntem takılabilir. Ücretsiz lisans kullanıcıları için Unity bu tür otomatik girişte arada engel koyabiliyor; olursa log'u bana at.
