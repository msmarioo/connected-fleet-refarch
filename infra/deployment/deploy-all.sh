#!/bin/bash

# Exit immediately if a command exits with a non-zero status
set -e

# Function to log in to Azure
azure_login() {
    if ! az account show > /dev/null 2>&1; then
    echo "Logging in to Azure..."
        az login --use-device-code
    else
        echo "Already logged in to Azure."
    fi
}

# Function to create resource groups
create_resource_groups() {
    echo "Creating resource groups..."
    az group create --name ${RG_TELEMETRYPLATFORM} --location ${LOCATION} --output table
    az group create --name ${RG_FLEETINTEGRATION} --location ${LOCATION} --output table
}

# Function to deploy telemetry platform
deploy_telemetry_platform() {
    

    pushd ./TelemetryPlatform

    if [ ! -f "./cert-gen/certs/azure-mqtt-test-only.chain.ca.cert.pem" ]; then

        # Make scripts executable
        
        chmod 700 generate-root-certificate.sh
        chmod 700 generate-client-certificates.sh
        chmod 700 ./cert-gen/certGen.sh

        # Generate certificates
        echo "Creating certificates..."
        ./generate-root-certificate.sh
        ./generate-client-certificates.sh
    else
        echo "Certificates already exist. Skipping certificate creation..."
    fi

    # Deploy telemetry platform resources
    echo "Deploying telemetry platform..."
    az deployment group create --resource-group ${RG_TELEMETRYPLATFORM} --template-file ./main.bicep --output table

    popd

    # Deploy telemetry platform functions  
    pushd ../../src/TelemetryPlatform/Functions

    export tpfunctionapp=$(az functionapp list --query "[].name" --resource-group ${RG_TELEMETRYPLATFORM} --output tsv)
    func azure functionapp publish ${tpfunctionapp} --dotnet

    popd

}

# Function to deploy fleet integration layer
deploy_fleet_integration() {
    echo "Deploying fleet integration layer..."

    pushd ./FleetIntegration

    # Get the name of the event hub created in the telemetry platform
    export tpeventhubname=$(az eventhubs namespace list --resource-group ${RG_TELEMETRYPLATFORM} --query "[].name" --output tsv)

    # Deploy fleet integration resources
    az deployment group create --resource-group ${RG_FLEETINTEGRATION} --template-file ./main.bicep --parameters evhnsTelemetryPlatformNamespaceName=${tpeventhubname}  --output table

    popd

}

# Main script execution
main() {

    azure_login
    create_resource_groups
    deploy_telemetry_platform
    deploy_fleet_integration

    echo "Deployment completed successfully."
}

echo "This script will deploy the telemetry platform and fleet integration layer to Azure."
echo "Please ensure you have the necessary permissions to create resource groups and deploy resources."
echo "You will be prompted for the resource group names and location."


# Ask for parameters with default values
read -p "Enter the resource group name for telemetry platform [telemetryplatform]: " RG_TELEMETRYPLATFORM
RG_TELEMETRYPLATFORM=${RG_TELEMETRYPLATFORM:-telemetryplatform}

read -p "Enter the resource group name for fleet integration [fleetintegration]: " RG_FLEETINTEGRATION
RG_FLEETINTEGRATION=${RG_FLEETINTEGRATION:-fleetintegration}

read -p "Enter the location [eastus]: " LOCATION
LOCATION=${LOCATION:-eastus}

# Run the main function
main
