using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using AuthorizationLevel = Microsoft.Azure.Functions.Worker.AuthorizationLevel;
using DurableClientAttribute = Microsoft.Azure.Functions.Worker.DurableClientAttribute;
using HttpTriggerAttribute = Microsoft.Azure.Functions.Worker.HttpTriggerAttribute;
using OrchestrationTriggerAttribute = Microsoft.Azure.Functions.Worker.OrchestrationTriggerAttribute;

namespace Company.Function
{
    public class PedidoApprovalRequest : ISerializable
    {
        public int PedidoId { get; set; }
        public decimal Valor { get; set; }

        public EnumPedidoEtapa Etapa {get; set;}

        public PedidoApprovalRequest(int pedidoId, decimal valorPedido, EnumPedidoEtapa etapaAprovacao)
        {
            PedidoId = pedidoId;
            Valor = valorPedido;
            Etapa = etapaAprovacao;
        }

        public PedidoApprovalRequest() { }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("PedidoId", PedidoId);
            info.AddValue("Valor", Valor);
            info.AddValue("Etapa", Etapa.ToString());
        }
    }

    public enum EnumPedidoEtapa
    {
        [Description("PedidoCriado")]
        PedidoCriado = 1,
        [Description("PedidoEmAnalise")]
        PedidoEmAnalise = 2,
        [Description("PedidoAprovado")]
        PedidoAprovado = 3
    }

    public static class DurableFunctionsPedido
    {
        [FunctionName("DurableFunctionsPedido")]
        public static async Task<List<string>> RunOrchestrator(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            ILogger log = context.CreateReplaySafeLogger("DurableFunctionsPedido");

            var outputs = new List<string>();

            var parametros = context.GetInput<PedidoApprovalRequest>();
            var idPedido = parametros?.PedidoId;
            var valor = parametros?.Valor;         

            var infos = parametros;

            var pedidoCriado = await context.CallActivityAsync<bool>(nameof(ProcessarEtapaPedido), infos);
            outputs.Add("Pedido Criado: " + pedidoCriado.ToString());

            log.LogInformation($"Pedido... id {idPedido} valor {valor} etapa {infos.Etapa} resultado {pedidoCriado}");

            if(!pedidoCriado)
            {
                log.LogInformation("Nenhum pedido encontrado.");
            }

            var pedidoEmAnalise = false;
            if (pedidoCriado)
            {
                infos.Etapa = EnumPedidoEtapa.PedidoEmAnalise;

                pedidoEmAnalise = await context.CallActivityAsync<bool>(nameof(ProcessarEtapaPedido), infos);
                outputs.Add("Pedido Em Analise: " + pedidoEmAnalise.ToString());

                log.LogInformation($"Pedido... id {idPedido} valor {valor} etapa {infos.Etapa} resultado {pedidoEmAnalise}");
            }

            var pedidoAprovado = false;
            if (pedidoEmAnalise)
            {
                infos.Etapa = EnumPedidoEtapa.PedidoAprovado;

                pedidoAprovado = await context.CallActivityAsync<bool>(nameof(ProcessarEtapaPedido), infos);
                outputs.Add("Pedido Finalizado: " + pedidoAprovado.ToString());

                log.LogInformation($"Pedido... id {idPedido} valor {valor} etapa {infos.Etapa} resultado {pedidoAprovado}");
            }

            return outputs;
        }

        [FunctionName(nameof(ProcessarEtapaPedido))]
        public static bool ProcessarEtapaPedido([ActivityTrigger] PedidoApprovalRequest pedidos, ILogger logger)
        {
            logger.LogInformation("Processar Etapa Pedido.");

            var idPedido = pedidos?.PedidoId;
            var valor = pedidos?.Valor;
            var etapa = pedidos?.Etapa.ToString();

            logger.LogInformation($"Processando Aprovação do Pedido {idPedido}, valor {valor}, etapa {etapa}.");
        
            if(pedidos != null)
            {
                if (pedidos.Etapa == EnumPedidoEtapa.PedidoAprovado)
                    return true;
                else
                    return false;
            }

            return false;
        }

        [FunctionName("DurableFunctionsPedido_HttpStart")]
        public static async Task<HttpResponseData> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
            [DurableClient] DurableTaskClient starter,
            FunctionContext context)
        {
            ILogger log = context.GetLogger(nameof(HttpStart));

            log.LogInformation(nameof(HttpStart));

            var parametros = new PedidoApprovalRequest();
            parametros.PedidoId = Convert.ToInt32(req.Query["idPedido"]);
            parametros.Valor = Convert.ToDecimal(req.Query["valor"]);
            parametros.Etapa = EnumPedidoEtapa.PedidoCriado;

            // Function input comes from the request content.
            string instanceId = await starter.ScheduleNewOrchestrationInstanceAsync("DurableFunctionsPedido", parametros);

            log.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }
}