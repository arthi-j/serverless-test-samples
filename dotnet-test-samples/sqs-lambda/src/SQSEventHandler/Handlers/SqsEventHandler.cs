using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;

namespace SqsEventHandler.Handlers;

/// <summary>
/// This class abstracts the AWS interaction between Amazon Simple Queue Service (SQS) & AWS Lambda Function.
/// </summary>
/// <typeparam name="TMessage">A generic SQS Message Model Type</typeparam>
public abstract class SqsEventHandler<TMessage> where TMessage : class, new()
{
    private List<SQSBatchResponse.BatchItemFailure> _batchItemFailures;
    private readonly SQSBatchResponse _sqsBatchResponse;

    protected SqsEventHandler()
    {
        _sqsBatchResponse = new SQSBatchResponse();
    }

    /// <summary>
    /// This method is completely abstracted from AWS Infrastructure and is called for every message.
    /// </summary>
    /// <param name="message">SQS Message Object</param>
    /// <param name="lambdaContext">Lambda Context</param>
    /// <returns></returns>
    public abstract Task ProcessSqsMessage(TMessage message, ILambdaContext lambdaContext);

    /// <summary>
    /// This method is called for every Lambda invocation. This method takes in an SQS event object and creates
    /// an SQS Event adapter for processing the batch of SQS messages.
    /// </summary>
    /// <param name="sqsEvent">SQS Event received by the function handler</param>
    /// <param name="lambdaContext">Lambda Context</param>
    /// <returns></returns>
    public async Task<SQSBatchResponse> Handler(SQSEvent sqsEvent, ILambdaContext lambdaContext)
    {
        await ProcessEvent(sqsEvent, lambdaContext);

        // Set BatchItemFailures if any
        if (_batchItemFailures != null)
        {
            _sqsBatchResponse.BatchItemFailures = _batchItemFailures;
        }

        return _sqsBatchResponse;
    }

    /// <summary>
    /// This method abstracts the SQS Event for downstream processing.
    /// </summary>
    /// <param name="sqsEvent">SQS Event received by the function handler</param>
    /// <param name="lambdaContext">Lambda Context</param>
    private async Task ProcessEvent(SQSEvent sqsEvent, ILambdaContext lambdaContext)
    {
        var sqsMessages = sqsEvent.Records;
        var batchItemFailures = new List<SQSBatchResponse.BatchItemFailure>();

        foreach (var sqsMessage in sqsMessages)
        {
            try
            {
                lambdaContext.Logger.LogLine($"Processing {sqsMessage.EventSource} Message Id: {sqsMessage.MessageId}");

                var message = JsonSerializer.Deserialize<TMessage>(sqsMessage.Body);

                // This abstract method is implemented by the concrete classes i.e. ProcessEmployeeFunction.
                await ProcessSqsMessage(message, lambdaContext);
            }
            catch (Exception ex)
            {
                lambdaContext.Logger.LogError($"Exception: {ex.Message}");
                batchItemFailures.Add(
                    new SQSBatchResponse.BatchItemFailure
                    {
                        ItemIdentifier = sqsMessage.MessageId
                    }
                );
            }
        }

        _batchItemFailures = batchItemFailures;
    }
}