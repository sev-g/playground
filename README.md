# Introduction

Welcome to the Playground repository. 

This repository is a collection of projects that demonstrate the integration and deployment of .NET 8 APIs on a Kubernetes cluster managed by Azure Kubernetes Service (AKS) using Pulumi as the infrastructure-as-code tool.

## Projects

The repository contains the following projects:

- **.NET 8 APIs**: two separate API projects built using the .NET 8 framework. These APIs serve as the primary workloads for deployment.

    - **Service A**: exposes the current value of Bitcoin in USD, updated every 10 seconds taken from an API on the web, and the average value over the last 10 minutes.

    - **Service B**:  exposes a single endpoint that responds with the 200 status code on GET requests.

- **Pulumi Project**: a Pulumi-based infrastructure-as-code setup that provisions and configures an AKS cluster with RBAC (Role-Based Access Control) enabled. This project handles the deployment of the above-mentioned .NET 8 APIs into the AKS environment.

## Prerequisites

- **.NET 8 SDK**: required for building and running the .NET APIs.
- **Pulumi**: needed for deploying infrastructure and workloads.
- **Azure CLI**: utilized for interacting with Azure services.
- **Docker**: necessary for containerizing the .NET APIs.
- **Kubectl**: for interacting with the Kubernetes cluster.
- **Kubelogin**: for AKS AAD login.

## Setup and Installation

1. Clone the Repository:

    ```bash
    git clone https://github.com/your-username/playground.git
    cd playground
    ```

2. Build .NET APIs:

    Navigate to each API project directory and run:

    ```
    dotnet build
    ```

3. Containerize APIs:

    Build Docker images for each API and push them to your container registry.

4. Set Up Pulumi:

    Navigate to the Pulumi project directory and install dependencies:

    ```
    pulumi up
    ```

5. Deploy to AKS:

    Use Pulumi scripts to deploy the .NET APIs as workloads on the AKS cluster.

## Usage

Once deployed, the APIs will be accessible through their corresponding ingress endpoints. Refer to the AKS and Pulumi documentation for details on accessing and managing deployed services.

## Contributing

Contributions to the Playground repository are welcome. Please follow the standard Git workflow - fork the repository, create a feature branch, and submit a pull request for review.

## License

This project is licensed under the MIT License - see the LICENSE.md file for details.