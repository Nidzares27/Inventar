# Google Login Setup

`Inventar.Storefront` sada podrzava Google login kao opcioni ulaz u korisnicki nalog.

## 1. Google Cloud Console

1. Otvorite [Google Cloud Console](https://console.cloud.google.com/).
2. Kreirajte ili izaberite projekat.
3. Ukljucite `Google People API` ili ostavite standardni OAuth consent setup za `email` i `profile` scope-ove.
4. Otvorite `APIs & Services > Credentials`.
5. Kreirajte `OAuth client ID`.
6. Kao tip aplikacije izaberite `Web application`.

## 2. Authorized redirect URI

Dodajte redirect URI koji odgovara callback putanji iz konfiguracije.

Podrazumijevana callback putanja u aplikaciji je:

`/signin-google-storefront`

Primjeri:

- lokalno: `http://localhost:5240/signin-google-storefront`
- produkcija: `https://vas-domen.com/signin-google-storefront`

Ako koristite drugi domen ili port, unesite tacan URL koji aplikacija zaista koristi.

## 3. User secrets

Iz storefront projekta pokrenite:

```powershell
cd "C:\Users\PC\OneDrive\Desktop\Inventar-Stabilna Verzija\Inventar.Storefront"
dotnet user-secrets set "StorefrontGoogleAuth:ClientId" "GOOGLE_CLIENT_ID"
dotnet user-secrets set "StorefrontGoogleAuth:ClientSecret" "GOOGLE_CLIENT_SECRET"
```

Po potrebi mozete promijeniti callback putanju:

```powershell
dotnet user-secrets set "StorefrontGoogleAuth:CallbackPath" "/signin-google-storefront"
```

## 4. Restart aplikacije

Nakon unosa kredencijala restartujte `Inventar.Storefront`. Kada su `ClientId` i `ClientSecret` popunjeni, na login stranici ce se automatski pojaviti Google dugme.

## 5. Kako radi flow

- korisnik klikne `Nastavi uz Google`
- Google vraca verifikovan email
- storefront pronalazi ili kreira `StorefrontCustomer` po toj email adresi
- korisnik se prijavljuje u svoj nalog
- ako profil nije kompletan, storefront ga vodi na `Profil` da dopuni podatke za dostavu
