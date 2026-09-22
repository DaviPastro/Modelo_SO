# Simulador de Sistemas Operacionais

Projeto da primeira entrega da disciplina. Simula processos e threads usando um relógio lógico, eventos e o escalonador FCFS.

## Como executar

No diretório do projeto:

dotnet run --project tests/OsSimulator.Tests
dotnet run --project src/OsSimulator.Cli -- workloads/fcfs-basico.json

É necessário ter o .NET SDK 10 instalado.

## O que foi entregue

* Processos e threads simulados.
* Fila de eventos e relógio lógico.
* Escalonamento FCFS.
* Leitura de carga JSON.
* Log e testes iniciais.
* Diagrama de classes: UML

