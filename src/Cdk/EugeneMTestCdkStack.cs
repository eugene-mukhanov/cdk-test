using System.Collections.Generic;
using System.IO;
using Amazon.CDK;
using Amazon.CDK.AWS.APIGateway;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Lambda.EventSources;
using Amazon.CDK.AWS.Route53.Targets;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.SQS;
using Constructs;
using IIntegrationResponse = Amazon.CDK.AWS.APIGateway.IIntegrationResponse;

namespace Cdk
{
    public class EugeneMTestCdkStack : Stack
    {
        internal EugeneMTestCdkStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            var apiGateWaySqs = new Queue(this, "apiGateWaySqs");

            var credentialsRole = new Role(this, "EugeneMTestRole",
                new RoleProps() { AssumedBy = new ServicePrincipal("apigateway.amazonaws.com") });

            apiGateWaySqs.GrantSendMessages(credentialsRole);
            
            var getReceiptStatusLambda = new Function(this, "GetReceiptStatusLambda", new FunctionProps
            {
                Runtime = Runtime.NODEJS_16_X,
                Code = Code.FromAsset("src/Cdk/resources"),
                Handler = "index.main",
            });
            
            var receiptProcessorLambda = new Function(this, "ReceiptProcessorLambda", new FunctionProps
            {
                Runtime = Runtime.NODEJS_18_X,
                Code = Code.FromAsset("resources"),
                Handler = "widgets.main",
            });

            var receiptProcessorLambdaEventSource = new SqsEventSource(apiGateWaySqs);
            receiptProcessorLambda.AddEventSource(receiptProcessorLambdaEventSource);
            
            var api = new RestApi(this, "EugeneM-Receipts-API", new RestApiProps
            {
                RestApiName = "Receipts-API",
                Description = "Receipts-API"
                
                
            });
            
            var receiptsResource = api.Root.AddResource("receipts");
            var createReceiptResource = receiptsResource.AddResource("{id}");
            createReceiptResource.AddMethod("POST", new AwsIntegration(new AwsIntegrationProps()
                {
                    Service = "sqs",
                    Path = $"{Account}/{apiGateWaySqs.QueueName}",
                    IntegrationHttpMethod = "POST",
                    Options = new IntegrationOptions()
                    {
                        CredentialsRole = credentialsRole,
                        RequestParameters = new Dictionary<string, string>()
                            { { "integration.request.header.Content-Type", "'application/x-www-form-urlencoded'" } },
                        RequestTemplates = new Dictionary<string, string>()
                            { { "application/json", "Action=SendMessage&MessageBody=$input.body" } },
                        IntegrationResponses = new IIntegrationResponse[]
                        {
                            new IntegrationResponse()
                            {
                                StatusCode = "200",
                                ResponseTemplates = new Dictionary<string, string>()
                                {
                                    { "application/json", "{\"done\": true}" }
                                }
                            }
                        },
                    },

                }),
                new MethodOptions()
                {
                    MethodResponses = new IMethodResponse[] { new MethodResponse() { StatusCode = "200" } },
                    RequestParameters = new Dictionary<string, bool>()
                    {
                        { "method.request.path.id", true }
                    },
                    RequestValidatorOptions = new RequestValidatorOptions() { ValidateRequestParameters = true }
                }
            );

            createReceiptResource.AddMethod("GET", new LambdaIntegration(getReceiptStatusLambda) { },
                new MethodOptions()
                {
                    RequestParameters = new Dictionary<string, bool>()
                    {
                        { "method.request.path.id", true }
                    },
                    RequestValidatorOptions = new RequestValidatorOptions() { ValidateRequestParameters = true }
                }
            );




            //api.Root.AddProxy(new ProxyResourceOptions()
            //{
            //    AnyMethod = true,
            //    DefaultIntegration = new LambdaIntegration(getReceiptStatusLambda)
            //
            //});


            /*new Bucket(this, "EugeneMyFirstBucket", new BucketProps
            {
                Versioned = true
            });*/
        }
    }
}
