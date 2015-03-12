﻿using UnityEngine;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;

public class GeneticLG : RandomLG 
{	
	private int _genomeIdx, _generationIdx;
	private int _sameBestFitnessCount;
	private bool _isRankingGenome;
	private float _lastGenerationBestFitness;
	
	private const string _logFile = "Assets/Experiments/expressivity100_50.txt";
	private const int _blocksMaxAmount = 100;
	
	// Experiments variables
	private int _fitnessEvaluation;
	private int _fitnessRecovered;
	
	private GeneticAlgorithm<AngryBirdsGen> _geneticAlgorithm;
	
	// Hash table used to cache fitness calculation
	private Dictionary<AngryBirdsGen, float> _fitnessCache;
	private AngryBirdsGen _lastgenome;
	
	// Fitness function parameters
	public float _bn, _d;
	
	// Genetic Algorithm parameters
	public int _populationSize, _generationSize;
	public float _mutationRate, _crossoverRate;
	public bool _elitism;
	
	public int _experimentsAmount = 1;
	private static float _lastExperimentTime;
	private static int _experimentsIdx;
	private static string _logContent;
	
	public override ABLevel GenerateLevel()
	{
		return new ABLevel();
	}
	
	public override void Start()
	{
		 base.Start();
		 
		_fitnessCache = new Dictionary<AngryBirdsGen, float>();

		// Generate a population of feaseble levels evaluated by an inteligent agent
		_geneticAlgorithm = new GeneticAlgorithm<AngryBirdsGen>(_crossoverRate, _mutationRate, _populationSize, _generationSize, _elitism);
		_geneticAlgorithm.InitGenome = new GeneticAlgorithm<AngryBirdsGen>.GAInitGenome(InitAngryBirdsGenome);
		_geneticAlgorithm.Mutation = new GeneticAlgorithm<AngryBirdsGen>.GAMutation(Mutate);
		_geneticAlgorithm.Crossover = new GeneticAlgorithm<AngryBirdsGen>.GACrossover(Crossover);
		_geneticAlgorithm.FitnessFunction = new GeneticAlgorithm<AngryBirdsGen>.GAFitnessFunction(EvaluateUsingAI);	
		_geneticAlgorithm.StartEvolution();

		_isRankingGenome = false;
		_generationIdx = 0;
		_genomeIdx = 0;

		// Set time scale to acelerate evolution
		Time.timeScale = 100f;
		
		// Disable audio
		 AudioListener.volume = 0f;
		 
		 // Totally zoom out
		 GameWorld.Instance._camera.SetCameraWidth(Mathf.Infinity);
		
		 // Remove all objects from level before start
		 GameWorld.Instance.ClearWorld();
	}

	void Update()
	{
		if(!_isRankingGenome)
		{
			float fitness = 0f;
			_geneticAlgorithm.GetNthGenome(_genomeIdx, out _lastgenome, out fitness);

			_fitnessEvaluation++;

			if(!_fitnessCache.ContainsKey(_lastgenome))
				StartEvaluatingGenome();
			else
			{
				_genomeIdx++;
				_isRankingGenome = false;
				_fitnessRecovered++;
			}
		}
		else if(GameWorld.Instance.IsLevelStable() && 
		       (GameWorld.Instance.GetPigsAvailableAmount()  == 0 || 
				GameWorld.Instance.GetBirdsAvailableAmount() == 0 ))
		{
			EndEvaluatingGenome();
			GameWorld.Instance.ClearWorld();

			_genomeIdx++;
			_isRankingGenome = false;
		}

		if(_genomeIdx == _geneticAlgorithm.PopulationSize)
		{
			Debug.Log("====== GENERATION " + _generationIdx +  " ======");
			
			_geneticAlgorithm.RankPopulation();
			float bestFitness = CheckStopCriteria();
			
			if(_generationIdx < _geneticAlgorithm.Generations) // && _sameBestFitnessCount < 20 && bestFitness != 1f)
			
				_geneticAlgorithm.CreateNextGeneration();
			else
				EndEvolution();
			
			_genomeIdx = 0;
			_generationIdx++;
		}
	}
	
	private void StartEvaluatingGenome()
	{
		DecodeLevel(ConvertShiftGBtoABGB(_lastgenome.gameObjects), _lastgenome.birdsAmount);
		_isRankingGenome = true;
	}
	
	private void EndEvaluatingGenome()
	{
		float bi = GameWorld.Instance.BirdsAtStart();
		float pi = GameWorld.Instance.PigsAtStart();
		float li = GameWorld.Instance.BlocksAtStart();
 
		float bk = GameWorld.Instance.GetBirdsAvailableAmount();
		float pk = GameWorld.Instance.GetPigsAvailableAmount();
		float lk = GameWorld.Instance.GetBlocksAvailableAmount();
		float sk = GameWorld.Instance.StabilityUntilFirstBird();
				
		float fitness = Fitness(pk, pi, li, lk, bi, bk, _d, sk);
		_fitnessCache.Add(_lastgenome, fitness);
	}
	
	private float Fitness(float pk, float pi, float li, float lk, float bi, float bk, float d, float sk)
	{						
		float distBirds = Mathf.Abs(Mathf.Ceil(_bn * bi) - (bi - bk));
		float distAmountBlocks = Mathf.Abs((Mathf.Ceil(d*_blocksMaxAmount) - li));
		
		return 1f/(1f + (distBirds + distAmountBlocks + sk + pk));
	}

	public float EvaluateUsingAI(AngryBirdsGen genome, int genomeIdx)
	{
		return _fitnessCache[genome];
	}
	
	private void EndEvolution()
	{
		Debug.Log("Experiment n: " + _experimentsIdx);
		
		if(_experimentsIdx < _experimentsAmount)
		{
			// Save the results
			SaveLog();
			
			// Run next experiment
			Application.LoadLevel(Application.loadedLevel);
		}
		else
		{		
			float fitness = 0f;
			AngryBirdsGen genome = new AngryBirdsGen();
			_geneticAlgorithm.GetBest(out genome, out fitness);
			
			// Save results
			WriteLogToFile(_logFile);
		
			// Default time scale
			Time.timeScale = 1f;
		
			// Enable audio
		 	AudioListener.volume = 1f;
		
			// Clear the level and decode the best genome of the last generation
			GameWorld.Instance.ClearWorld();			
			DecodeLevel(ConvertShiftGBtoABGB(genome.gameObjects), genome.birdsAmount);				
							
			// Disable AI and allow player to test the level
			GameWorld.Instance._birdAgent.gameObject.SetActive(false);
		
			// Disable simulation
			GameWorld.Instance._isSimulation = false;
		
			// Destroy the generator
			Destroy(this.gameObject);
		}
		
		_experimentsIdx++;
	}
	
	private float CheckStopCriteria() 
	{
		float fitness = 0f;
		AngryBirdsGen genome = new AngryBirdsGen();
		_geneticAlgorithm.GetBest(out genome, out fitness);

		if(_lastGenerationBestFitness == fitness)
			
			_sameBestFitnessCount++;
		else
			_sameBestFitnessCount = 0;
		
		_lastGenerationBestFitness = (float)fitness;

		UnityEngine.Debug.Log("Best fitness " + fitness);
		UnityEngine.Debug.Log("Same Best fitness Count " + _sameBestFitnessCount);
		UnityEngine.Debug.Log("Cache size " + _fitnessCache.Count);
		UnityEngine.Debug.Log("Cache usage " + _fitnessRecovered);
		
		return fitness;
	}
	
	public void Crossover(ref Genome<AngryBirdsGen> genome1, ref Genome<AngryBirdsGen> genome2, 
	                      out Genome<AngryBirdsGen> child1,  out Genome<AngryBirdsGen> child2) {

		child1 = new Genome<AngryBirdsGen>();
		child2 = new Genome<AngryBirdsGen>();

		AngryBirdsGen genes1 = new AngryBirdsGen();
		AngryBirdsGen genes2 = new AngryBirdsGen();
		
		if(UnityEngine.Random.value <= _geneticAlgorithm.CrossoverRate)
		{	
			int maxGenomeSize = Mathf.Max (genome1.Genes.gameObjects.Count, 
			                               genome2.Genes.gameObjects.Count);
			
			for(int i = 0; i < maxGenomeSize; i++)
			{	
				if(genome1.Genes.gameObjects.Count == genome2.Genes.gameObjects.Count)
				{				
					if(UnityEngine.Random.value < 0.5f)
							genes1.gameObjects.Add(CopyStack(genome1.Genes.gameObjects[i]));
					else
							genes1.gameObjects.Add(CopyStack(genome2.Genes.gameObjects[i]));

					if(UnityEngine.Random.value < 0.5f)
							genes2.gameObjects.Add(CopyStack(genome1.Genes.gameObjects[i]));
					else
							genes2.gameObjects.Add(CopyStack(genome2.Genes.gameObjects[i]));
				}
				else if(genome1.Genes.gameObjects.Count < genome2.Genes.gameObjects.Count)
				{
					if(i < genome1.Genes.gameObjects.Count)
					{					
						if(UnityEngine.Random.value < 0.5f)
								genes1.gameObjects.Add(CopyStack(genome1.Genes.gameObjects[i]));
						else
								genes1.gameObjects.Add(CopyStack(genome2.Genes.gameObjects[i]));

				
						if(UnityEngine.Random.value < 0.5f)
								genes2.gameObjects.Add(CopyStack(genome1.Genes.gameObjects[i]));
						else
								genes2.gameObjects.Add(CopyStack(genome2.Genes.gameObjects[i]));
					}
					else
					{					
						if(UnityEngine.Random.value < 0.5f)
								genes1.gameObjects.Add(CopyStack(genome2.Genes.gameObjects[i]));
					
						if(UnityEngine.Random.value < 0.5f)
								genes2.gameObjects.Add(CopyStack(genome2.Genes.gameObjects[i]));
					}
				}
				else
				{
					if(i < genome2.Genes.gameObjects.Count)
					{	
						if(UnityEngine.Random.value < 0.5f)
								genes1.gameObjects.Add(CopyStack(genome1.Genes.gameObjects[i]));
						else
								genes1.gameObjects.Add(CopyStack(genome2.Genes.gameObjects[i]));

						if(UnityEngine.Random.value < 0.5f)
								genes2.gameObjects.Add(CopyStack(genome1.Genes.gameObjects[i]));
						else
								genes2.gameObjects.Add(CopyStack(genome2.Genes.gameObjects[i]));
					}
					else
					{				
						if(UnityEngine.Random.value < 0.5f)
								genes1.gameObjects.Add(CopyStack(genome1.Genes.gameObjects[i]));
				
						if(UnityEngine.Random.value < 0.5f)
								genes2.gameObjects.Add(CopyStack(genome1.Genes.gameObjects[i]));
					}
				}
			}
		
			// Integer crossover for birds
			genes1.birdsAmount = (int)(0.5f * genome1.Genes.birdsAmount + 0.5f * genome2.Genes.birdsAmount);
			genes2.birdsAmount = (int)(1.5f * genome1.Genes.birdsAmount - 0.5f * genome2.Genes.birdsAmount);
		}
		else
		{
			for(int i = 0; i < genome1.Genes.gameObjects.Count; i++)
			{	
				genes1.gameObjects.Add(CopyStack(genome1.Genes.gameObjects[i]));
			}
			
			genes1.birdsAmount = genome1.Genes.birdsAmount;
			
			for(int i = 0; i < genome2.Genes.gameObjects.Count; i++)
			{
				genes2.gameObjects.Add(CopyStack(genome2.Genes.gameObjects[i]));	
			}
			
			genes2.birdsAmount = genome2.Genes.birdsAmount;
		}
		
		FixLevelSize(ref genes1.gameObjects);
		FixLevelSize(ref genes2.gameObjects);
		
		child1.Genes = genes1;
		child2.Genes = genes2;
	}
	
	public void Mutate(ref Genome<AngryBirdsGen> genome) {

		for(int i = 0; i < genome.Genes.gameObjects.Count; i++)
		{
			if(UnityEngine.Random.value <= _geneticAlgorithm.MutationRate)
			{
				genome.Genes.gameObjects[i].Clear();
				genome.Genes.gameObjects[i] = new LinkedList<ShiftABGameObject>();
				
				// Generate new stacks
				GenerateNextStack(i, ref genome.Genes.gameObjects);
				InsertPigs(i, ref genome.Genes.gameObjects);
			}
		}
		
		if(UnityEngine.Random.value <= _geneticAlgorithm.MutationRate)
			genome.Genes.birdsAmount = UnityEngine.Random.Range(0, _birdsMaxAmount);
		
		FixLevelSize(ref genome.Genes.gameObjects);
	}

	public void InitAngryBirdsGenome(out AngryBirdsGen genome) {

		genome = new AngryBirdsGen();

		genome.birdsAmount = UnityEngine.Random.Range(0, _birdsMaxAmount);
		genome.gameObjects = GenerateRandomLevel();
	}
	
	private void SaveLog()
	{
		float fitness = 0f;
		AngryBirdsGen genome = new AngryBirdsGen();
		_geneticAlgorithm.GetBest(out genome, out fitness);
		
		float experimentTime = Time.realtimeSinceStartup - _lastExperimentTime;
		
		_logContent += "====== RESULTS ======\n";
		_logContent += "Execution time: "       + experimentTime + "\n";
		_logContent += "Convergence: "          + _generationIdx + "\n";
		_logContent += "Cache size:"            + _fitnessCache.Count + "\n";
		_logContent += "Fitness calculations: " + _fitnessEvaluation + "\n";
		_logContent += "Fitness recovered: "    + _fitnessRecovered + "\n";
		_logContent += "Best Fitness: "         + fitness + "\n";
		_logContent += "Linearity: "            + GetLevelLinearity(genome.gameObjects) + "\n";
		_logContent += "Density: "              + GetLevelDensity(genome.gameObjects) + "\n";
		_logContent += "Frequency pig: "        + GetABGameObjectFrequency(ConvertShiftGBtoABGB(genome.gameObjects), GameWorld.Instance.Templates.Length) + "\n";;	
		_logContent += "Frequency bird: "       + GetBirdsFrequency(genome.birdsAmount) + "\n";;	
		
		_lastExperimentTime = Time.realtimeSinceStartup;		
	}
	
	private void WriteLogToFile(string filename)
	{
		StreamWriter writer = new StreamWriter(filename); // Does this work?
		writer.WriteLine(_logContent);
		writer.Close();
	}
	
	private void OnApplicationQuit() 
	{
		WriteLogToFile(_logFile);
	}
}
