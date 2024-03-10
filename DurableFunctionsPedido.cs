using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace Company.Function
{
    public class PedidoApprovalRequest
    {
        public int PedidoId { get; set; }
        public decimal Valor { get; set; }

        public bool Resultado {get; set;}

        public EnumPedidoEtapa Etapa {get; set;}

        public PedidoApprovalRequest(int idPedido, decimal valorPedido, EnumPedidoEtapa etapaAprovacao)
        {
            PedidoId = idPedido;
            Valor = valorPedido;
            Etapa = etapaAprovacao;
        }

        public PedidoApprovalRequest()
        {
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
        [FunctionName("PedidoFuncOrchestration")]
        public static async Task<List<string>> RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            //ILogger logger = context.CreateReplaySafeLogger();
            var outputs = new List<string>();

            var parametros = context.GetInput<PedidoApprovalRequest>();
            var idPedido = parametros?.PedidoId;
            var valor = parametros?.Valor;         

            var infos = parametros;

            // Replace "hello" with the name of your Durable Activity Function.
            var pedidoCriado = await context.CallActivityAsync<bool>(nameof(ProcessarEtapaPedido), infos);
            outputs.Add("PedidoCriado " + pedidoCriado.ToString());
            
            //logger.LogInformation($"Aprovação de Pedido... id {idPedido} valor {valor} etapa {dados.Etapa} resultado {resultadoPedidoCriado}");

            return outputs;
        }

        [FunctionName("PedidoApproval")]
        public static string SayHello([ActivityTrigger] string name, ILogger log)
        {
            log.LogInformation("Saying hello to {name}.", name);
            return $"Hello {name}!";
        }

        [FunctionName(nameof(ProcessarEtapaPedido))]
        public static bool ProcessarEtapaPedido([ActivityTrigger] PedidoApprovalRequest pedidos, ILogger logger)
        {
            logger.LogInformation("Processar Etapa Pedido.");

            var idPedido = pedidos.PedidoId;
            var valor = pedidos.Valor;
            var etapa = pedidos.Etapa.ToString();

            logger.LogInformation($"Processando Aprovação do Pedido {idPedido}, valor {valor}, etapa {etapa}.");
        
            if (pedidos.Etapa == EnumPedidoEtapa.PedidoAprovado)
                return true;
            else
                return false;
        }

        [FunctionName("DurableFunctionsPedido_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync("DurableFunctionsPedido", null);

            log.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }
}