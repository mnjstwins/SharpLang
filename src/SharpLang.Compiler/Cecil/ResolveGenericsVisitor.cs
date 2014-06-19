﻿using System;
using System.Collections.Generic;
using Mono.Cecil;

namespace SharpLang.CompilerServices.Cecil
{
    /// <summary>
    /// Transform open generic types to closed instantiation using context information.
    /// See <see cref="Process"/> for more details.
    /// </summary>
    class ResolveGenericsVisitor : TypeReferenceVisitor
    {
        private Dictionary<TypeReference, TypeReference> genericTypeMapping;

        public ResolveGenericsVisitor(Dictionary<TypeReference, TypeReference> genericTypeMapping)
        {
            this.genericTypeMapping = genericTypeMapping;
        }

        /// <summary>
        /// Transform open generic types to closed instantiation using context information.
        /// As an example, if B{T} inherits from A{T}, running it with B{C} as context and A{B.T} as type, ti will return A{C}.
        /// </summary>
        public static TypeReference Process(TypeReference context, TypeReference type)
        {
            if (type == null)
                return null;

            var genericInstanceTypeContext = context as GenericInstanceType;
            if (genericInstanceTypeContext == null)
                return type;

            // Build dictionary that will map generic type to their real implementation type
            var resolvedType = genericInstanceTypeContext.ElementType;
            var genericTypeMapping = new Dictionary<TypeReference, TypeReference>();
            for (int i = 0; i < resolvedType.GenericParameters.Count; ++i)
            {
                var genericParameter = genericInstanceTypeContext.ElementType.GenericParameters[i];
                genericTypeMapping.Add(genericParameter, genericInstanceTypeContext.GenericArguments[i]);
            }

            var visitor = new ResolveGenericsVisitor(genericTypeMapping);
            var result = visitor.VisitDynamic(type);

            // Make sure type is closed now
            if (result.ContainsGenericParameter())
                throw new InvalidOperationException("Unsupported generic resolution.");

            return result;
        }

        public static TypeReference Process(MethodReference context, TypeReference type)
        {
            if (type == null)
                return null;

            if (context == null)
                return type;

            var genericInstanceTypeContext = context.DeclaringType as GenericInstanceType;
            var genericInstanceMethodContext = context as GenericInstanceMethod;
            if (genericInstanceMethodContext == null && genericInstanceTypeContext == null)
                return type;

            // Build dictionary that will map generic type to their real implementation type
            var genericTypeMapping = new Dictionary<TypeReference, TypeReference>();
            if (genericInstanceTypeContext != null)
            {
                var resolvedType = genericInstanceTypeContext.ElementType;
                for (int i = 0; i < resolvedType.GenericParameters.Count; ++i)
                {
                    var genericParameter = genericInstanceTypeContext.ElementType.GenericParameters[i];
                    genericTypeMapping.Add(genericParameter, genericInstanceTypeContext.GenericArguments[i]);
                }
            }

            if (genericInstanceMethodContext != null)
            {
                // TODO: Only scanning declaring types generic parameters, need to add method's one too
                var elementMethod = genericInstanceMethodContext.ElementMethod;
                for (int i = 0; i < elementMethod.GenericParameters.Count; ++i)
                {
                    var genericParameter = elementMethod.GenericParameters[i];
                    genericTypeMapping.Add(genericParameter, genericInstanceMethodContext.GenericArguments[i]);
                }
            }

            var visitor = new ResolveGenericsVisitor(genericTypeMapping);
            var result = visitor.VisitDynamic(type);

            // Make sure type is closed now
            //if (result.ContainsGenericParameter())
            //    throw new InvalidOperationException("Unsupported generic resolution.");

            return result;
        }

        public static MethodReference Process(MethodReference method)
        {
            var reference = new MethodReference(method.Name, Process(method, method.ReturnType), Process(method, method.DeclaringType))
            {
                HasThis = method.HasThis,
                ExplicitThis = method.ExplicitThis,
                CallingConvention = method.CallingConvention,
            };

            foreach (var parameter in method.Parameters)
                reference.Parameters.Add(new ParameterDefinition(Process(method, parameter.ParameterType)));

            foreach (var generic_parameter in method.GenericParameters)
                reference.GenericParameters.Add(new GenericParameter(generic_parameter.Name, reference));

            return reference;
        }

        public static bool ContainsGenericParameters(MethodReference method)
        {
            // Determine if method contains any open generic type.
            // TODO: Might need a more robust generic resolver/analyzer system soon.
            method = Process(method);
            return method.ContainsGenericParameter();
        }

        public override TypeReference Visit(GenericParameter type)
        {
            TypeReference result;
            if (genericTypeMapping.TryGetValue(type, out result))
                return result;

            return base.Visit(type);
        }
    }
}