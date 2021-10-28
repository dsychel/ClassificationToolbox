#pragma once
#include<vector>

int OMP_NUM_THR = 8;
int PROGRESS_BUFFER = 1000;

//string errorLogPath = "unmanagedErrorLog";
//ofstream errorStream(errorLogPath);

using namespace std;

enum ERRORS
{
	UNKNOWN_CLASSIFIER = -1,
	UNKNOWN_EXTRACTOR = -2,
	UNSUPPORTED_IMAGE_FORMAT = -3,
	CORRUPTED_FEATURES_FILE = -4,
	CORRUPTED_CLASSIFIER_FILE = -5,
	CORRUPTED_FILE = -6,
	INCORRECT_METRICES = -7,
	INCONSISTENT_FEATURES = -8,
	INCONSISTENT_WEIGHTS = -9,
	OPERATION_CANCELED = -10,
	NOT_IMPLEMENTED = -100,
	UNKNOWN_ERROR = -1000,
};

/// <summary>Paramtery dla klasyfikatorow</summary>
struct ClassifierParameters
{
	static const int STRING_BUFFER = 256;
	// Casacde parameters
	int cascadeStages = 10; /// <summary>Maksymalna liczba etapow w kaskadzie</summary>
	double maxFAR = 0.01; /// <summary>Wartosc FAR jaka musi zostac osiagnieta w wyniku uczenia kaskady</summary>	
	double minSpecificity = 0.9; /// <summary>Wartosc specyficznosci jaka nie moze zostac przekroczona a w wyniku uczenia kaskady</summary>
	char boostingType[STRING_BUFFER] = "AdaBoost"; /// <summary>Typ boostingu jaki zostanie wykorzystany w kaskadzie</summary>
	char learningMethod[10] = "UGM-G";
	char nonFaceImagesPath[STRING_BUFFER * 2] = "";
	int childsCount = 3;
	int splits = 1;
	bool isGraph = false;
	bool isDijkstra = false;
	bool isUniform = false;
	double pruningFactor = 0.0;

	// Seeds
	bool forceSeeds = true;
	int validSeed1 = 12801;
	int validSeed2 = 1021;
	int trainSeed1 = 151;
	int trainSeed2 = 3457;
	bool resizeSetsWhenResampling = true;
	int resamplingMaxValSize = 25000;
	int resamplingMaxTrainSize1 =  50000;
	int resamplingMaxTrainSize2 = 35000;

	// Extractor parameters;
	char extractorType[STRING_BUFFER] = "HaarExtractor";
	int p = 8;
	int q = 8;
	int r = 6;
	int rt = 1;
	int w = 200;
	int d = 200;
	int t = 6;
	int s = 7;
	int ps = 7;
	int b = 8;
	int nx = 5;
	int ny = 5;

	// Resampling
	int repetitionPerImage = 5000;
	int resScales = 5;
	int minWindow = 48;
	double jumpingFactor = 0.05;
	double scaleFactor = 1.2;

	// Boosting parameters	
	int boostingStages = 300; /// <summary>Maksymalna liczba etapow w boostingu</summary>
	int realBoostBins = 32; /// <summary>Liczba koszy dla algorytmu RealBoost</summary>
	bool useWeightTrimming = false; /// <summary>Czy wlaczyc wygaszanie wag?</summary>
	double weightTrimmingThreshold = 0.99; /// <summary>Wymagana wartosc masy prawdopodobienstwa</summary>
	double weightTrimmingMinSamples = 0.01; /// <summary>Minimalny procent probek, ktory musi uczestniczyc w nauce</summary>
	char classifierType[STRING_BUFFER] = "DecisionStump"; /// <summary>Typ slabego klasyfikatora jaki zostanie wykorzystany w boostingu</summary>

														  // Classifier parameters
	int maxIterations = 150; /// <summary>Maksymalna liczba iteracji dla slabego klasyfikatora (np. Perceptronu)</summary>
	double learningRate = 0.1; /// <summary>Wspolcznynik uczenia dla slabego klasyfikaotra (np. Perceptronu)</summary>
	int maxTreeLevel = 3; /// <summary>Maksymalna poziom drzewa</summary>	
	int treeBins = 8;/// <summary>Liczba koszy na poziomie drzewa</summary>
	char impurityMetric[STRING_BUFFER] = "Gini"; /// <summary>Wykorzystana miara zanieczyszczenia</summary>

												 // Other
	double outlayerPercent = 0.0;
};

/// <summary>Paramtery dla detekcji</summary>
struct DetectionParameters
{
	int windowMinimalWidth = 48;
	int windowMinimalHeight = 48;
	int scales = 5;
	double windowScalingRatio = 1.2;
	double windowJumpingRatio = 0.05;
};