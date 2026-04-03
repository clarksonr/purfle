@description('Purfle IdentityHub.Web — Azure App Service (F1 free tier)')

param location string = resourceGroup().location
param appName string = 'purfle-identityhub-web'

@secure()
param adminToken string

param marketplaceApiUrl string
param identityHubApiUrl string

@secure()
param storageConnectionString string

// Reuse existing App Service Plan or create a new one
param planName string = 'purfle-marketplace-plan'

resource existingPlan 'Microsoft.Web/serverfarms@2023-12-01' existing = {
  name: planName
}

// App Service
resource app 'Microsoft.Web/sites@2023-12-01' = {
  name: appName
  location: location
  properties: {
    serverFarmId: existingPlan.id
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      appSettings: [
        {
          name: 'PURFLE_ADMIN_TOKEN'
          value: adminToken
        }
        {
          name: 'Marketplace__BaseUrl'
          value: marketplaceApiUrl
        }
        {
          name: 'IdentityHub__BaseUrl'
          value: identityHubApiUrl
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

output identityHubWebUrl string = 'https://${app.properties.defaultHostName}'
