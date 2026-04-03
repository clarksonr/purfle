@description('Purfle Marketplace API — Azure App Service (F1 free tier)')

param location string = resourceGroup().location
param appName string = 'purfle-marketplace'

@secure()
param adminToken string

@secure()
param storageConnectionString string

// App Service Plan — F1 free tier
resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: '${appName}-plan'
  location: location
  sku: {
    name: 'F1'
    tier: 'Free'
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

// App Service
resource app 'Microsoft.Web/sites@2023-12-01' = {
  name: appName
  location: location
  properties: {
    serverFarmId: plan.id
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      appSettings: [
        {
          name: 'PURFLE_ADMIN_TOKEN'
          value: adminToken
        }
        {
          name: 'AZURE_STORAGE_CONNECTION_STRING'
          value: storageConnectionString
        }
      ]
    }
    httpsOnly: true
  }
}

output marketplaceUrl string = 'https://${app.properties.defaultHostName}'
