using Pulumi;
using Pulumi.Kubernetes;
using Pulumi.Kubernetes.Helm;
using Pulumi.Kubernetes.Types.Inputs.Core.V1;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;
using Pulumi.Kubernetes.Types.Inputs.Networking.V1;
using System;
using System.Collections.Generic;
using System.Text;
using AzureNative = Pulumi.AzureNative;

return await Pulumi.Deployment.RunAsync(() =>
{
    // Grab some values from the Pulumi stack configuration (or use defaults)
    var projCfg = new Pulumi.Config();
    var numWorkerNodes = projCfg.GetInt32("numWorkerNodes") ?? 3;
    var k8sVersion = projCfg.Get("kubernetesVersion") ?? "1.26.3";
    var prefixForDns = projCfg.Get("prefixForDns") ?? "pulumi";
    var nodeVmSize = projCfg.Get("nodeVmSize") ?? "Standard_DS2_v2";

    // The next two configuration values are required (no default can be provided)
    var mgmtGroupId = projCfg.Require("mgmtGroupId");
    var sshPubKey = projCfg.Require("sshPubKey");

    // Create a new Azure Resource Group
    var resourceGroup = new AzureNative.Resources.ResourceGroup("resourceGroup");

    // Create a new Azure Virtual Network
    var virtualNetwork = new AzureNative.Network.VirtualNetwork("virtualNetwork", new()
    {
        AddressSpace = new AzureNative.Network.Inputs.AddressSpaceArgs
        {
            AddressPrefixes = new[]
            {
                "10.0.0.0/16",
            },
        },
        ResourceGroupName = resourceGroup.Name,
    });

    // Create three subnets in the virtual network
    var subnet1 = new AzureNative.Network.Subnet("subnet1", new()
    {
        AddressPrefix = "10.0.0.0/22",
        ResourceGroupName = resourceGroup.Name,
        VirtualNetworkName = virtualNetwork.Name,
    });

    var subnet2 = new AzureNative.Network.Subnet("subnet2", new()
    {
        AddressPrefix = "10.0.4.0/22",
        ResourceGroupName = resourceGroup.Name,
        VirtualNetworkName = virtualNetwork.Name,
    });

    var subnet3 = new AzureNative.Network.Subnet("subnet3", new()
    {
        AddressPrefix = "10.0.8.0/22",
        ResourceGroupName = resourceGroup.Name,
        VirtualNetworkName = virtualNetwork.Name,
    });

    // Create an Azure Kubernetes Cluster
    var managedCluster = new AzureNative.ContainerService.ManagedCluster("managedCluster", new()
    {
        AadProfile = new AzureNative.ContainerService.Inputs.ManagedClusterAADProfileArgs
        {
            EnableAzureRBAC = true,
            Managed = true,
            AdminGroupObjectIDs = new[]
            {
                mgmtGroupId,
            },
        },
        AddonProfiles = { },
        // Use multiple agent/node pool profiles to distribute nodes across subnets
        AgentPoolProfiles = new AzureNative.ContainerService.Inputs.ManagedClusterAgentPoolProfileArgs
        {
            AvailabilityZones = new[]
            {
                "1", "2", "3",
            },
            Count = numWorkerNodes,
            EnableNodePublicIP = false,
            Mode = "System",
            Name = "systempool",
            OsType = "Linux",
            OsDiskSizeGB = 30,
            Type = "VirtualMachineScaleSets",
            VmSize = nodeVmSize,
            // Change next line for additional node pools to distribute across subnets
            VnetSubnetID = subnet1.Id,
        },

        // Change authorizedIPRanges to limit access to API server
        // Changing enablePrivateCluster requires alternate access to API server (VPN or similar)
        ApiServerAccessProfile = new AzureNative.ContainerService.Inputs.ManagedClusterAPIServerAccessProfileArgs
        {
            AuthorizedIPRanges = new[]
            {
                "0.0.0.0/0",
            },
            EnablePrivateCluster = false,
        },
        DnsPrefix = prefixForDns,
        EnableRBAC = true,
        Identity = new AzureNative.ContainerService.Inputs.ManagedClusterIdentityArgs
        {
            Type = AzureNative.ContainerService.ResourceIdentityType.SystemAssigned,
        },
        KubernetesVersion = k8sVersion,
        LinuxProfile = new AzureNative.ContainerService.Inputs.ContainerServiceLinuxProfileArgs
        {
            AdminUsername = "azureuser",
            Ssh = new AzureNative.ContainerService.Inputs.ContainerServiceSshConfigurationArgs
            {
                PublicKeys = new[]
                {
                    new AzureNative.ContainerService.Inputs.ContainerServiceSshPublicKeyArgs
                    {
                        KeyData = sshPubKey,
                    },
                },
            },
        },
        NetworkProfile = new AzureNative.ContainerService.Inputs.ContainerServiceNetworkProfileArgs
        {
            NetworkPlugin = "azure",
            NetworkPolicy = "azure",
            ServiceCidr = "10.96.0.0/16",
            DnsServiceIP = "10.96.0.10",
        },
        ResourceGroupName = resourceGroup.Name,
    });

    // Build a Kubeconfig to access the cluster
    var creds = AzureNative.ContainerService.ListManagedClusterUserCredentials.Invoke(new()
    {
        ResourceGroupName = resourceGroup.Name,
        ResourceName = managedCluster.Name,
    });

    var encoded = creds.Apply(result => result.Kubeconfigs[0]!.Value);

    var decoded = encoded.Apply(enc =>
    {
        var bytes = Convert.FromBase64String(enc);
        return Encoding.UTF8.GetString(bytes);
    });

    var k8sProvider = new Provider("k8sProvider", new ProviderArgs
    {
        KubeConfig = decoded
    });

    var namespaceName = "ingress-nginx";
    var ingressNamespace = new Pulumi.Kubernetes.Core.V1.Namespace("ingress-nginx", new Pulumi.Kubernetes.Types.Inputs.Core.V1.NamespaceArgs
    {
        Metadata = new ObjectMetaArgs
        {
            Name = namespaceName
        },
    }, new CustomResourceOptions { Provider = k8sProvider });

    var ingressController = new Pulumi.Kubernetes.Helm.V3.Chart("ingress-nginx", new Pulumi.Kubernetes.Helm.ChartArgs
    {
        Namespace = ingressNamespace.Metadata.Apply(metadata => metadata.Name),
        FetchOptions = new ChartFetchArgs
        {
            Repo = "https://kubernetes.github.io/ingress-nginx",
        },
        Chart = "ingress-nginx",
        Version = "3.7.1",
    }, new ComponentResourceOptions { Provider = k8sProvider });

    var serviceA = new Pulumi.Kubernetes.Core.V1.Service("serviceA", new ServiceArgs
    {
        Metadata = new ObjectMetaArgs
        {
            Name = "service-a",
        },
        Spec = new ServiceSpecArgs
        {
            Selector = new Dictionary<string, string>
                {
                    { "app", "serviceA" },
                },
            Ports = new ServicePortArgs[]
                {
                    new ServicePortArgs
                    {
                        Port = 80,
                        TargetPort = 8080
                    },
                },
        },
    }, new CustomResourceOptions { Provider = k8sProvider });

    var serviceB = new Pulumi.Kubernetes.Core.V1.Service("serviceB", new ServiceArgs
    {
        Metadata = new ObjectMetaArgs
        {
            Name = "service-b",
        },
        Spec = new ServiceSpecArgs
        {
            Selector = new Dictionary<string, string>
                {
                    { "app", "serviceB" },
                },
            Ports = new ServicePortArgs[]
                {
                    new ServicePortArgs
                    {
                        Port = 80,
                        TargetPort = 8080
                    },
                },
        },
    }, new CustomResourceOptions { Provider = k8sProvider });

    var deploymentA = new Pulumi.Kubernetes.Apps.V1.Deployment("deploymentA", new Pulumi.Kubernetes.Types.Inputs.Apps.V1.DeploymentArgs
    {
        Metadata = new ObjectMetaArgs
        {
            Name = "service-a-deployment",
        },
        Spec = new Pulumi.Kubernetes.Types.Inputs.Apps.V1.DeploymentSpecArgs
        {
            Selector = new LabelSelectorArgs
            {
                MatchLabels = new Dictionary<string, string>
                    {
                        { "app", "serviceA" },
                    }
            },
            Template = new PodTemplateSpecArgs
            {
                Metadata = new ObjectMetaArgs
                {
                    Labels = new Dictionary<string, string>
                        {
                            { "app", "serviceA" },
                        }
                },
                Spec = new PodSpecArgs
                {
                    Containers = new ContainerArgs[]
                        {
                            new ContainerArgs
                            {
                                Name = "service-a-container",
                                Image = "sevg/service-a:latest",
                            }
                        }
                }
            }
        },
    }, new CustomResourceOptions { Provider = k8sProvider });

    var deploymentB = new Pulumi.Kubernetes.Apps.V1.Deployment("deploymentB", new Pulumi.Kubernetes.Types.Inputs.Apps.V1.DeploymentArgs
    {
        Metadata = new ObjectMetaArgs
        {
            Name = "service-b-deployment",
        },
        Spec = new Pulumi.Kubernetes.Types.Inputs.Apps.V1.DeploymentSpecArgs
        {
            Selector = new LabelSelectorArgs
            {
                MatchLabels = new Dictionary<string, string>
                    {
                        { "app", "serviceB" },
                    }
            },
            Template = new PodTemplateSpecArgs
            {
                Metadata = new ObjectMetaArgs
                {
                    Labels = new Dictionary<string, string>
                        {
                            { "app", "serviceB" },
                        }
                },
                Spec = new PodSpecArgs
                {
                    Containers = new ContainerArgs[]
                        {
                            new ContainerArgs
                            {
                                Name = "service-b-container",
                                Image = "sevg/service-b:latest",
                            }
                        }
                }
            }
        },
    }, new CustomResourceOptions { Provider = k8sProvider });

    var ingressA = new Pulumi.Kubernetes.Networking.V1.Ingress("ingressA", new IngressArgs
    {
        Metadata = new ObjectMetaArgs
        {
            Name = "ingress-a",
            Annotations =
            {
                { "kubernetes.io/ingress.class", "nginx" }, // Necessary for nginx ingress
                //{ "nginx.ingress.kubernetes.io/rewrite-target", "/" },
            },
        },
        Spec = new IngressSpecArgs
        {
            Rules = new IngressRuleArgs[]
                {
                    new IngressRuleArgs
                    {
                        Http = new HTTPIngressRuleValueArgs
                        {
                            Paths = new HTTPIngressPathArgs[]
                            {
                                new HTTPIngressPathArgs
                                {
                                    Path = "/",
                                    PathType = "Prefix",
                                    Backend = new IngressBackendArgs
                                    {
                                        Service = new IngressServiceBackendArgs
                                        {
                                            Name = serviceA.Metadata.Apply(metadata => metadata.Name),
                                            Port = new ServiceBackendPortArgs
                                            {
                                                Number = 80,
                                            },
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
        },
    }, new CustomResourceOptions { Provider = k8sProvider });

    var ingressB = new Pulumi.Kubernetes.Networking.V1.Ingress("ingressB", new IngressArgs
    {
        Metadata = new ObjectMetaArgs
        {
            Name = "ingress-b",
            Annotations =
            {
                { "kubernetes.io/ingress.class", "nginx" }, // Necessary for nginx ingress
                //{ "nginx.ingress.kubernetes.io/rewrite-target", "/" },
            },
        },
        Spec = new IngressSpecArgs
        {
            Rules = new IngressRuleArgs[]
               {
                    new IngressRuleArgs
                    {
                        Http = new HTTPIngressRuleValueArgs
                        {
                            Paths = new HTTPIngressPathArgs[]
                            {
                                new HTTPIngressPathArgs
                                {
                                    Path = "/b",
                                    PathType = "Prefix",
                                    Backend = new IngressBackendArgs
                                    {
                                        Service = new IngressServiceBackendArgs
                                        {
                                            Name = serviceB.Metadata.Apply(metadata => metadata.Name),
                                            Port = new ServiceBackendPortArgs
                                            {
                                                Number = 80,
                                            },
                                        },
                                    },
                                },
                            },
                        },
                    },
               },
        },
    }, new CustomResourceOptions { Provider = k8sProvider });

    // Export some values for use elsewhere
    return new Dictionary<string, object?>
    {
        ["rgName"] = resourceGroup.Name,
        ["networkName"] = virtualNetwork.Name,
        ["clusterName"] = managedCluster.Name,
        ["kubeconfig"] = decoded,
    };
});
