﻿using System;
using System.IO;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using PowerArgs;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using System.IO.Packaging;
using System.Net.Mime;
using System.Diagnostics;

namespace MetadataClient
{
    public class Arguments
    {
        [ArgActionMethod]
        public void UploadPackage(UploadPackageArgs args)
        {
            if (String.IsNullOrEmpty(args.ContainerName))
            {
                args.ContainerName = "received";
            }

            if (String.IsNullOrEmpty(args.CacheControl))
            {
                args.CacheControl = "no-cache";
            }

            if (String.IsNullOrEmpty(args.ContentType))
            {
                args.ContentType = "application/octet-stream";
            }

            CloudStorageAccount account = CloudStorageAccount.Parse(args.StorageConnectionString);
            CloudBlobClient client = account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(args.ContainerName);

            if(container.CreateIfNotExists())
            {
                Console.WriteLine("Created '{0}' received container", args.ContainerName);
            }

            string filename = args.Path.Substring(args.Path.LastIndexOf('\\') + 1);

            CloudBlockBlob blob = container.GetBlockBlobReference(filename);

            blob.Properties.CacheControl = args.CacheControl;

            blob.Properties.ContentType = args.ContentType;

            blob.UploadFromFile(args.Path, FileMode.Open);
        }

        [ArgActionMethod]
        public void GenMetadata(MakeMetadataArgs args)
        {
            if (String.IsNullOrEmpty(args.ReceivedContainer))
            {
                args.ReceivedContainer = "received";
            }

            if (String.IsNullOrEmpty(args.PublishContainer))
            {
                args.PublishContainer = "pub";
            }

            MakeMetadata.Program.Run(args.StorageConnectionString, args.ReceivedContainer, args.PublishContainer);
        }

        [ArgActionMethod]
        public void RenameOwner(RenameOwnerArgs args)
        {
            throw new NotImplementedException();
        }

        [ArgActionMethod]
        public void Pack(PackArgs args)
        {
            string filename = args.Nuspec;

            FileStream stream = new FileStream(filename, FileMode.Open);
            XDocument nuspec = XDocument.Load(stream);

            IXmlNamespaceResolver resolver = CreateNamespaceResolver();

            string id = nuspec.XPathSelectElement("nuget:package/nuget:metadata/nuget:id", resolver).Value;
            string version = nuspec.XPathSelectElement("nuget:package/nuget:metadata/nuget:version", resolver).Value;

            Uri partUri = PackUriHelper.CreatePartUri(new Uri("/" + id + ".nuspec", UriKind.Relative));

            string packagePath = id + "." + version + ".nupkg";

            using (Package package = Package.Open(packagePath, FileMode.Create))
            {
                PackagePart packagePart = package.CreatePart(partUri, MediaTypeNames.Text.Xml);

                using (XmlWriter writer = XmlWriter.Create(packagePart.GetStream(), new XmlWriterSettings { Indent = true }))
                {
                    nuspec.WriteTo(writer);
                }
            }
        }

        static string Nuget = "http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd";

        static IXmlNamespaceResolver CreateNamespaceResolver()
        {
            XmlNamespaceManager namespaceManager = new XmlNamespaceManager(new NameTable());
            namespaceManager.AddNamespace("nuget", Nuget);
            return namespaceManager;
        }
    }
}
