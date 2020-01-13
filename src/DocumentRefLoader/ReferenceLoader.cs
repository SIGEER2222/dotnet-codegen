﻿using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using DocumentRefLoader.Settings;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YamlDotNet.Serialization;

namespace DocumentRefLoader
{
    /// <summary>
    /// https://swagger.io/docs/specification/using-ref/
    /// </summary>
    public sealed class ReferenceLoader
    {
        internal readonly Dictionary<Uri, ReferenceLoader> _otherLoaders;

        private readonly IReferenceLoaderSettings _settings;
        internal readonly Uri _documentUri;
        internal readonly Uri _documentFolder;
        private readonly string _originalDocument;
        private readonly JObject _rootJObj;

        public ReferenceLoader(string fileUri, ReferenceLoaderStrategy strategy)
            : this(fileUri.GetAbsoluteUri(), null, strategy)
        { }

        private ReferenceLoader(Uri documentUri, Dictionary<Uri, ReferenceLoader> otherLoaders, ReferenceLoaderStrategy strategy)
            : this(documentUri, otherLoaders, strategy.GetSettings())
        { }

        private ReferenceLoader(Uri documentUri, Dictionary<Uri, ReferenceLoader> otherLoaders, IReferenceLoaderSettings settings)
        {
            _documentUri = documentUri.GetAbsolute();
            _documentFolder = documentUri.GetFolder();

            _otherLoaders = otherLoaders ?? new Dictionary<Uri, ReferenceLoader>() { { _documentUri, this } };
            _settings = settings;

            _originalDocument = _documentUri.DownloadDocumentAsync().Result;
            _rootJObj = _settings.Deserialise(_originalDocument, documentUri.ToString());
            OriginalJson = _rootJObj.ToString();
        }


        internal string OriginalJson { get; }
        internal string FinalJson => _rootJObj.ToString();



        public string GetRefResolvedYaml() => _settings.YamlSerialize(GetRefResolvedJObject());
        public string GetRefResolvedJson() => _settings.JsonSerialize(GetRefResolvedJObject());

        public JObject GetRefResolvedJObject()
        {
            EnsureRefResolved();
            return _rootJObj;
        }

        bool _isResolved = false;
        private void EnsureRefResolved()
        {
            if (_isResolved)
                return;

            _isResolved = true;
            ResolveRef(_rootJObj);
        }

        private void ResolveRef(JToken token)
        {
            if (!(token is JContainer container))
                return;

            var refProps = container.Descendants()
                .OfType<JProperty>()
                .Where(p => p.Name == Constants.REF_KEYWORD)
                .ToList();

            foreach (var refProperty in refProps)
            {
                var refPath = refProperty.Value.ToString();
                var refInfo = RefInfo.GetRefInfo(_documentUri, refPath);

              

                if (!_settings.ShouldResolveReference(refInfo)) continue;

                var replacement = GetRefJToken(refInfo);
                ResolveRef(replacement);

                if (refProperty.Parent?.Parent == null)
                    // When a property has already been replaced by recursions, it has no more Parent
                    continue;

                // clone to avoid destroying the original documents
                replacement = replacement.DeepClone();

                _settings.ApplyRefReplacement(refInfo, _rootJObj, refProperty, replacement, refInfo.AbsoluteDocumentUri);
            }
        }

        private JToken GetRefJToken(RefInfo refInfo)
        {
            // TODO : Be able to load external (http) documents with credentials (swagger hub)
            var loader = GetReferenceLoader(refInfo);
            var replacement = loader.GetDocumentPart(refInfo.InDocumentPath);
            return replacement;
        }

        private JToken GetDocumentPart(string path)
        {
            EnsureRefResolved();

            var parts = path.Split(new[] { "/" }, StringSplitOptions.RemoveEmptyEntries);

            JToken token = _rootJObj;
            foreach (var part in parts)
            {
                if (!string.IsNullOrWhiteSpace(part))
                {
                    try
                    {
                        token = token[part];
                    }
                    catch (Exception)
                    {
                        throw new InvalidDataException($"Unable to find path '{path}' in the document '{_documentUri}'.");
                    }
                }
            }

            return token;
        }

        private ReferenceLoader GetReferenceLoader(RefInfo refInfo)
        {
            if (refInfo.IsNestedInThisDocument)
                return this;

            if (_otherLoaders.TryGetValue(refInfo.AbsoluteDocumentUri, out var loader))
                return loader;

            loader = new ReferenceLoader(refInfo.AbsoluteDocumentUri, _otherLoaders, _settings);
            _otherLoaders[refInfo.AbsoluteDocumentUri] = loader;
            return loader;
        }
    }
}
