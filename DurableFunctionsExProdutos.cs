using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace Company.Function
{
    public static class DurableFunctionsExProdutos
    {
        public class PedidoApprovalRequest : ISerializable
        {
            public int PedidoId { get; set; }
            public decimal Valor { get; set; }

            public EnumPedidoEtapa Etapa { get; set; }

            public PedidoApprovalRequest(int pedidoId, decimal valor, EnumPedidoEtapa etapa)
            {
                PedidoId = pedidoId;
                Valor = valor;
                Etapa = etapa;
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

        [FunctionName("DurableFunctionsExProdutos")]
        public static async Task<List<string>> RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            context.SetCustomStatus("DurableFunctionsExProdutos");

            var outputs = new List<string>();

            var parametros = context.GetInput<PedidoApprovalRequest>();
            var pedidoId = parametros?.PedidoId;
            var valor = parametros?.Valor;

            var infos = parametros;

            var pedidoCriado = await context.CallActivityAsync<bool>(nameof(ProcessarEtapaPedido), infos);
            outputs.Add("Pedido Criado: " + pedidoCriado.ToString());

            context.SetCustomStatus($"Pedido... id: {pedidoId} valor: {valor} etapa: {infos.Etapa}");

            if (!pedidoCriado)
            {
                context.SetCustomStatus("Nenhum pedido encontrado.");
            }

            var pedidoEmAnalise = false;
            if (pedidoCriado)
            {
                infos.Etapa = EnumPedidoEtapa.PedidoEmAnalise;

                pedidoEmAnalise = await context.CallActivityAsync<bool>(nameof(ProcessarEtapaPedido), infos);
                outputs.Add("Pedido Em Analise: " + pedidoEmAnalise.ToString());

                context.SetCustomStatus($"Pedido... id: {pedidoId} valor: {valor} etapa: {infos.Etapa}");
            }

            var pedidoAprovado = false;
            if (pedidoEmAnalise)
            {
                infos.Etapa = EnumPedidoEtapa.PedidoAprovado;

                pedidoAprovado = await context.CallActivityAsync<bool>(nameof(ProcessarEtapaPedido), infos);
                outputs.Add("Pedido Finalizado: " + pedidoAprovado.ToString());

                context.SetCustomStatus($"Pedido... id: {pedidoId} valor: {valor} etapa: {infos.Etapa}");
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

            if (pedidos != null)
            {
                if (pedidos.Etapa == EnumPedidoEtapa.PedidoCriado)
                    return true;
                else if (pedidos.Etapa == EnumPedidoEtapa.PedidoEmAnalise)
                    return true;
                else if (pedidos.Etapa == EnumPedidoEtapa.PedidoAprovado)
                    return true;
                else
                    return false;
            }

            return false;
        }

        [FunctionName("DurableFunctionsExProdutos_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            var parametros = new PedidoApprovalRequest();

            var queryParameters = req.RequestUri.ParseQueryString();

            parametros.PedidoId = Convert.ToInt32(queryParameters["PedidoId"]);
            parametros.Valor = Convert.ToDecimal(queryParameters["Valor"]);
            parametros.Etapa = EnumPedidoEtapa.PedidoCriado;

            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync("DurableFunctionsExProdutos", parametros);

            log.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }
}