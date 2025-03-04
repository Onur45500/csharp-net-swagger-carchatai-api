# CarChat API

## Présentation du projet

CarChat API est une application C# .NET qui permet de rechercher des véhicules sur Leboncoin et d'interagir avec les résultats. Cette API offre une interface simple pour effectuer des recherches avancées de véhicules en utilisant divers critères tels que la marque, le modèle, le prix, le type de carburant, le nombre de portes, le nombre de places, etc.

L'API utilise des techniques de web scraping pour récupérer les données de Leboncoin et les présenter dans un format structuré et facilement exploitable.

## Fonctionnalités principales

- Recherche de véhicules avec filtres avancés (marque, modèle, prix, carburant, etc.)
- Extraction des attributs des véhicules (kilométrage, année, type de carburant, boîte de vitesse, etc.)
- Pagination des résultats (jusqu'à 10 pages)
- Extraction d'images de haute qualité
- Analyse des descriptions pour extraire des informations supplémentaires
- Comparaison de véhicules pour faciliter la prise de décision
- Filtrage avancé des résultats de recherche

## Prérequis

- .NET 9.0 ou supérieur
- Visual Studio 2022 ou un autre IDE compatible avec .NET
- Accès à Internet pour les requêtes vers Leboncoin

## Installation

1. Clonez le dépôt :
   ```
   git clone https://github.com/votre-utilisateur/csharp-net-swagger-carchat-api.git
   cd csharp-net-swagger-carchat-api
   ```

2. Restaurez les packages NuGet :
   ```
   dotnet restore
   ```

3. Compilez le projet :
   ```
   dotnet build
   ```

## Lancement de l'application

Pour lancer l'application en mode développement :

```
dotnet run
```

L'API sera accessible à l'adresse : `https://localhost:5001` ou `http://localhost:5000`

Pour accéder à la documentation Swagger de l'API :
```
https://localhost:5001/swagger
```

## Endpoints de l'API

L'API expose les endpoints suivants :

### 1. Recherche de véhicules

```
POST /api/chat/search
```

Permet de rechercher des véhicules sur Leboncoin en fonction des critères spécifiés.

**Paramètres de la requête :**
```json
{
  "brand": "string",           // Marque du véhicule (ex: Renault, Peugeot)
  "model": "string",           // Modèle du véhicule (ex: Clio, 308)
  "minPrice": integer,         // Prix minimum en euros
  "maxPrice": integer,         // Prix maximum en euros
  "location": "string",        // Localisation (ville ou code postal)
  "fuelType": "string",        // Type de carburant (Essence, Diesel, Électrique, Hybride)
  "regDate": "string",         // Année d'immatriculation
  "vehicleTypes": ["string"],  // Types de véhicule (Citadine, SUV, Berline, etc.)
  "doors": "string",           // Nombre de portes (3, 4, 5)
  "seats": "string",           // Nombre de places (2, 4, 5, 6, 7+)
  "keywords": "string"         // Mots-clés supplémentaires pour la recherche
}
```

**Réponse :**
Liste des annonces de véhicules correspondant aux critères de recherche.

### 2. Obtenir l'URL de recherche

```
POST /api/chat/buildurl
```

Construit et retourne l'URL de recherche Leboncoin sans effectuer le scraping.

**Paramètres :** Identiques à l'endpoint de recherche.

**Réponse :**
```json
{
  "url": "string"  // URL de recherche Leboncoin
}
```

### 3. Scraper une URL spécifique

```
POST /api/chat/scrape
```

Effectue le scraping d'une URL Leboncoin spécifique.

**Paramètres :**
```json
{
  "url": "string"  // URL Leboncoin à scraper
}
```

**Réponse :**
Liste des annonces de véhicules trouvées à l'URL spécifiée.

### 4. Filtrer les résultats

```
POST /api/chat/filter
```

Filtre une liste d'annonces de véhicules selon des critères spécifiques.

**Paramètres :**
```json
{
  "articles": [
    // Liste des articles à filtrer
  ],
  "filters": {
    "minPrice": integer,         // Prix minimum
    "maxPrice": integer,         // Prix maximum
    "minYear": integer,          // Année minimum
    "maxYear": integer,          // Année maximum
    "minKm": integer,            // Kilométrage minimum
    "maxKm": integer,            // Kilométrage maximum
    "fuelTypes": ["string"],     // Types de carburant acceptés
    "transmissionTypes": ["string"], // Types de transmission acceptés
    "keywords": ["string"]       // Mots-clés à rechercher dans le titre ou la description
  }
}
```

**Réponse :**
Liste filtrée des annonces de véhicules.

### 5. Comparer des véhicules

```
POST /api/chat/compare
```

Compare plusieurs véhicules et met en évidence leurs différences.

**Paramètres :**
```json
{
  "articleIds": [
    "string",  // IDs des articles à comparer
    "string"
  ]
}
```

**Réponse :**
```json
{
  "comparison": {
    "commonAttributes": {
      // Attributs communs à tous les véhicules
    },
    "differences": [
      {
        "articleId": "string",
        "attributes": {
          // Attributs spécifiques à ce véhicule
        }
      }
    ],
    "priceComparison": {
      "lowestPrice": {
        "articleId": "string",
        "price": number
      },
      "highestPrice": {
        "articleId": "string",
        "price": number
      },
      "averagePrice": number
    }
  }
}
```

### 6. Obtenir des recommandations

```
POST /api/chat/recommend
```

Fournit des recommandations de véhicules similaires à un véhicule donné.

**Paramètres :**
```json
{
  "articleId": "string",  // ID de l'article de référence
  "count": integer        // Nombre de recommandations souhaitées
}
```

**Réponse :**
Liste des véhicules recommandés, triés par pertinence.

## Utilisation de l'API

### Exemple de requête pour rechercher des véhicules

```http
POST /api/chat/search
Content-Type: application/json

{
  "brand": "Renault",
  "model": "Clio",
  "minPrice": 5000,
  "maxPrice": 15000,
  "fuelType": "Diesel",
  "vehicleTypes": ["Citadine"],
  "doors": "5",
  "seats": "5"
}
```

### Exemple de filtrage des résultats

```http
POST /api/chat/filter
Content-Type: application/json

{
  "articles": [
    // Liste des articles obtenus d'une recherche précédente
  ],
  "filters": {
    "minYear": 2018,
    "maxKm": 50000,
    "fuelTypes": ["Essence", "Hybride"],
    "keywords": ["Garantie", "Première main"]
  }
}
```

### Exemple de comparaison de véhicules

```http
POST /api/chat/compare
Content-Type: application/json

{
  "articleIds": [
    "12345678",
    "87654321",
    "24681357"
  ]
}
```

## Déploiement

Pour publier l'application pour un déploiement en production :

```
dotnet publish -c Release
```

Les fichiers publiés seront disponibles dans le dossier `bin/Release/net9.0/publish/`.

## Licence

Ce projet est sous licence MIT.
