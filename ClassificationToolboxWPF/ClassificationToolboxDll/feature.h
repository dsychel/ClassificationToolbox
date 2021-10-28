 #pragma once
#include<vector>
#include<numeric>
#include<algorithm>
#include<iostream>
#include<unordered_set>
#include<omp.h>
#include<complex>
#include<cstdio>
#define _USE_MATH_DEFINES
#include<math.h>

#include"utills.h"
#include"configuration.h"

typedef void(__stdcall * ProgressCallback)(int);

/// <summary>Klasa bazowa dla ekstraktorow cech</summary>
class Extractor
{
protected:
	static constexpr int pLimit = 20; // maskymalne mozliwe p w gui
	static constexpr int qLimit = 20; // maksymalne mozliwe q w gui
	static constexpr int predictedMaxWindowSize = 900; // jesli ta wartosc zostanie przekroczona speeder zostanie przeliczony
	static constexpr int predictedMaxSize = 450; // dla partial speeder
	static constexpr int predictedMinSize = -450;

	int featuresCount = 0;

	int width;
	int height;

	SaveFileType saveMode;

public:
	virtual ~Extractor() {}

	/// <summary>Zwraca typ cechy</summary>
	/// <returns>Typ klasyfikatora</returns>
	virtual string getType() const = 0;

	virtual bool getRectangleWindowsRequirement() const = 0;

	/// <summary>Zwraca opis ekstraktora cech</summary>
	/// <returns>Opis ekstraktora cech</returns>
	virtual string toString() const = 0;

	virtual void initializeExtractor(Point* sizes, int sca) = 0;

	virtual void loadImageData(const string path) = 0;

	virtual void loadImageData(const double* const* img, int height, int width) = 0;

	virtual void clearImageData() = 0;

	virtual int getWidth()
	{
		return width;
	}

	virtual int getHeight()
	{
		return height;
	}

	virtual int getFeaturesCount()
	{
		return featuresCount;
	}

	/// <summary>Ekstrachuje cechy z podanego obrazu</summary>
	/// <param name = 'Wx'>Szerokosc okna</param>
	/// <param name = 'Wy'>Wysokosc okna</param>
	/// <param name = 'xp'>X-pozycja okna (lewy gorny rog)</param>
	/// <param name = 'yp'>Y-pozycja okna (lewy gorny rog)</param>
	/// <returns>Ekstrachowane cechy</returns>
	virtual tuple<int, const double*> extractFromWindow(int Wx, int Wy, int xp = 0, int yp = 0) = 0;
	/// <summary>Ekstrachuje cechy z podanego obrazu</summary>
	/// <param name = 'featuresID'>Numery cech</param>
	/// <param name = 'Wx'>Szerokosc okna</param>
	/// <param name = 'Wy'>Wysokosc okna</param>
	/// <param name = 'xp'>X-pozycja okna (lewy gorny rog)</param>
	/// <param name = 'yp'>Y-pozycja okna (lewy gorny rog)</param>
	/// <returns>Ekstrachowane cechy</returns>
	virtual int extractFromWindow(double* features, const int* featuresID, int fLength, int Wx, int Wy, int xp = 0, int yp = 0) = 0;

	/// <summary>Ekstrachuje cechy z podanego pliku</summary>
	/// <param name = 'path'>Sciezka do pliku</param>
	/// <returns>Ekstrachowane cechy</returns>
	virtual tuple<int, const double*> extractFeatures(const string path)
	{
		loadImageData(path);

		int nx = width;
		int ny = height;

		if (this->getRectangleWindowsRequirement())
		{
			int minDim = min(width, height);
			if (minDim % 2 == 1)
				minDim--;
			nx = minDim;
			ny = minDim;
		}

		return extractFromWindow(nx, ny);
	}

	/// <summary>Ekstrachuje cechy z podanego pliku</summary>
	/// <param name = 'path'>Sciezka do pliku</param>
	/// <param name = 'featuresID'>Numery cech</param>
	/// <returns>Ekstrachowane cechy</returns>
	virtual int extractFeatures(double* features, const string path, const int* featuresID, int fLength)
	{
		loadImageData(path);

		int nx = width;
		int ny = height;

		if (this->getRectangleWindowsRequirement())
		{
			int minDim = min(width, height);
			if (minDim % 2 == 1)
				minDim--;
			nx = minDim;
			ny = minDim;
		}

		return extractFromWindow(features, featuresID, fLength, nx, ny);
	}

	/// <summary>Ekstrachuje cechy z podanych plikow</summary>
	/// <param name = 'paths'>Wektor sciezek do plikow</param>
	virtual tuple<int, int, const double* const*> extractMultipleFeatures(const vector<string> &paths) = 0;
};

class HaarExtractor : public Extractor
{
private:
	int scales; /// <summary>Liczba roznych rozmiarów dla pojedynczej cechy</summary>
	int density; /// <summary>Liczb molziwych polozen cechy w X oraz Y (liczba cech dla jednej skali = density ^ 2)</summary>
	int templatesCount;
	static const double halfOfSqrtFromTwo;

	map<int, int*> scale_fxi;
	map<int, int*> scale_fyi;
	map<int, double*> scale_hxi;
	map<int, double*> scale_hyi;

	double** ii = nullptr;

	double* powHSFT = nullptr;

	struct FeatureDescriptor
	{
		int templateNumber;
		int sx;
		int sy;
		int px;
		int py;
	};

	FeatureDescriptor* descriptions = nullptr;

	/// <summary>Struktura zawierajaca informacje o ksztalcie cechy</summary>
	struct HaarTemplate
	{
		vector<vector<double>> whiteAreas; /// <summary>Lista z obszarami wchodzacymi w sklad cechy (x1, y1, x2, y2)</summary>
		const double whiteAreasField;
		const double blackAreasField;
	};
	static constexpr int tCount = 6;
	static const HaarTemplate HaarFeaturesShapes[tCount]; /// <summary>Lista wykorzystywanych cech Haara</summary>

	/// <summary>Wyznacza obraz ca³kowy dla podanego obrazu</summary>
	/// <param name = 'img'>Obraz</param>
	/// <returns>Obraz ca³kowy</returns>
	void calculateIntegralImage(const double* const* img)
	{
		ii = new double*[height + 1];
		for (int i = 0; i <= height; i++)
			ii[i] = new double[width + 1];

		for (int i = 0; i <= height; i++)
			ii[i][0] = 0;
		for (int i = 0; i <= width; i++)
			ii[0][i] = 0;

		double* ll = new double[width + 1];
		ll[0] = 0;
		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				double a = img[y][x];
				double s = 0;
				if (x > 0)
					s = ll[x] + a;
				else
					s = a;
				ll[x + 1] = s;
				if (y > 0)
					s = s + ii[y][x + 1];
				ii[y + 1][x + 1] = s;
			}
		}
		delete[] ll;
	}

	HaarExtractor(HaarExtractor *parent)
	{
		this->scales = parent->scales;
		this->density = parent->density;
		this->templatesCount = parent->templatesCount;
		this->saveMode = parent->saveMode;
		this->featuresCount = parent->featuresCount;

		descriptions = new FeatureDescriptor[featuresCount];
		for (int i = 0; i < featuresCount; i++)
			this->descriptions[i] = parent->descriptions[i];

		this->powHSFT = new double[scales + 1];
		for (int s = 0; s <= scales; s++)
			this->powHSFT[s] = parent->powHSFT[s];

		for (map<int, int*>::iterator iter = scale_fxi.begin(); iter != scale_fxi.end(); ++iter)
		{
			int k = iter->first;

			this->scale_fxi[k] = new int[scales + 1];
			this->scale_hxi[k] = new double[scales + 1];

			for (int s = 1; s <= scales; s++)
			{
				this->scale_fxi[k][s] = parent->scale_fxi[k][s];
				this->scale_hxi[k][s] = parent->scale_hxi[k][s];
			}
		}

		for (map<int, int*>::iterator iter = scale_fyi.begin(); iter != scale_fyi.end(); ++iter)
		{
			int k = iter->first;

			this->scale_fyi[k] = new int[scales + 1];
			this->scale_hyi[k] = new double[scales + 1];

			for (int s = 1; s <= scales; s++)
			{
				this->scale_fyi[k][s] = parent->scale_fyi[k][s];
				this->scale_hyi[k][s] = parent->scale_hyi[k][s];
			}
		}
	}

public:
	using Extractor::extractFromWindow;

	~HaarExtractor()
	{
		delete[] powHSFT;

		for (map<int, int*>::iterator iter = scale_fxi.begin(); iter != scale_fxi.end(); ++iter)
		{
			int k = iter->first;
			delete[] scale_fxi[k];
			delete[] scale_hxi[k];
		}

		for (map<int, int*>::iterator iter = scale_fyi.begin(); iter != scale_fyi.end(); ++iter)
		{
			int k = iter->first;
			delete[] scale_fyi[k];
			delete[] scale_hyi[k];
		}

		scale_fxi.clear();
		scale_fyi.clear();
		scale_hxi.clear();
		scale_hyi.clear();

		if (ii != nullptr)
		{
			for (int i = 0; i <= height; i++)
				delete[] ii[i];
			delete[] ii;
		}
		delete[] descriptions;
	}

	HaarExtractor() : HaarExtractor(5, 5, 6, SaveFileType::text) {}

	/// <param name = 'scales'>Liczba roznych rozmiarów dla pojedynczej cech</param>
	/// <param name = 'density'>Liczb molziwych polozen cechy w X oraz Y (liczba cech dla jednej skali = density ^ 2)</param>
	HaarExtractor(int scales, int density, int templatesCount, SaveFileType fileType = SaveFileType::text)
	{
		this->scales = scales;
		this->density = density;
		this->saveMode = fileType;

		powHSFT = new double[scales + 1];
		for (int s = 1; s <= scales; s++)
			powHSFT[s] = pow(halfOfSqrtFromTwo, s);

		if (templatesCount > tCount)
			this->templatesCount = tCount;
		else
			this->templatesCount = templatesCount;

		featuresCount = (int)(pow(this->scales, 2) * pow(this->density, 2) * this->templatesCount);

		descriptions = new FeatureDescriptor[featuresCount];
		int f = 0;
		for (int t = 0; t < templatesCount; t++)
		{
			for (int sx = 1; sx <= scales; sx++)
			{
				for (int sy = 1; sy <= scales; sy++)
				{
					for (int px = 0; px < density; px++)
					{
						for (int py = 0; py < density; py++)
						{
							descriptions[f] = FeatureDescriptor{ t, sx, sy, px, py };
							f++;
						}
					}
				}
			}
		}
	}

	static string GetType()
	{
		return "HaarExtractor";
	}

	/// <summary>Zwraca typ cechy</summary>
	/// <returns>Typ klasyfikatora</returns>
	string getType() const override
	{
		return GetType();
	}

	bool getRectangleWindowsRequirement() const override
	{
		return false;
	}

	/// <summary>Zwraca opis ekstraktora cech</summary>
	/// <returns>Opis ekstraktora cech</returns>
	string toString() const override
	{
		string text = getType() + "\r\n";
		text += "Skale: " + to_string(scales) + "\r\n";
		text += "Positions: " + to_string(density) + " x " + to_string(density) + "\r\n";
		text += "Templates: " + to_string(templatesCount) + "\r\n";

		return text;
	}

	void loadImageData(const string path) override
	{
		if (ii != nullptr)
		{
			for (int i = 0; i <= height; i++)
				delete[] ii[i];
			delete[] ii;
			ii = nullptr;
		}

		auto[height, width, img] = loadImage(path, saveMode);
		this->width = width;
		this->height = height;

		calculateIntegralImage(img);

		for (int i = 0; i < height; i++)
			delete[] img[i];
		delete[] img;
	}

	void loadImageData(const double* const* img, int height, int width) override
	{
		if (ii != nullptr)
		{
			for (int i = 0; i <= height; i++)
				delete[] ii[i];
			delete[] ii;
			ii = nullptr;
		}

		this->width = width;
		this->height = height;

		calculateIntegralImage(img);
	}

	void clearImageData() override
	{
		if (ii != nullptr)
		{
			for (int i = 0; i <= height; i++)
				delete[] ii[i];
			delete[] ii;

			ii = nullptr;
		}
	}

	void initializeExtractor(Point* sizes, int sca) override
	{
		for (int s1 = 0; s1 < sca; s1++)
		{
			int Wx = sizes[s1].wx;
			int Wy = sizes[s1].wy;

			if (scale_fxi.count(Wx) == 0)
			{
				this->scale_fxi[Wx] = new int[scales + 1];
				this->scale_hxi[Wx] = new double[scales + 1];
				for (int s = 1; s <= scales; s++)
				{
					this->scale_fxi[Wx][s] = (int)(Wx * powHSFT[s]);
					this->scale_hxi[Wx][s] = (Wx - scale_fxi[Wx][s]) / (density - 1);
				}
			}

			if (scale_fyi.count(Wy) == 0)
			{
				this->scale_fyi[Wy] = new int[scales + 1];
				this->scale_hyi[Wy] = new double[scales + 1];
				for (int s = 1; s <= scales; s++)
				{
					this->scale_fyi[Wy][s] = (int)(Wy * powHSFT[s]);
					this->scale_hyi[Wy][s] = (Wy - scale_fyi[Wy][s]) / (density - 1);
				}
			}
		}
	}

	/// <param name = 'featuresID'>Numery cech</param>
	/// <param name = 'Wx'>Szerokosc okna</param>
	/// <param name = 'Wy'>Wysokosc okna</param>
	/// <param name = 'xp'>X-pozycja okna (lewy gorny rog)</param>
	/// <param name = 'yp'>Y-pozycja okna (lewy gorny rog)</param>
	int extractFromWindow(double* features, const int* featuresID, int fLength, int Wx, int Wy, int xp = 0, int yp = 0) override
	{
		int* fxi = scale_fxi[Wx];
		int* fyi = scale_fyi[Wy];
		double* hxi = scale_hxi[Wx];
		double* hyi = scale_hyi[Wy];

		for (int f = 0; f < fLength; f++)
		{
			int id = featuresID[f];

			const HaarTemplate& hTemplate = HaarFeaturesShapes[descriptions[id].templateNumber];

			int &fx = fxi[descriptions[id].sx];
			int &fy = fyi[descriptions[id].sy];

			int fxfy = fx * fy;

			int xl = xp + (int)(descriptions[id].px * hxi[descriptions[id].sx]);
			int xr = xl + fx;
			int yu = yp + (int)(descriptions[id].py * hyi[descriptions[id].sy]);
			int yd = yu + fy;

			double whiteAreaSum = 0;
			for (int i = 0; i < (int)hTemplate.whiteAreas.size(); i++)
			{
				int x1 = (int)(xl + fx * hTemplate.whiteAreas[i][0]);
				int y1 = (int)(yu + fy * hTemplate.whiteAreas[i][1]);
				int x2 = (int)(xl + fx * hTemplate.whiteAreas[i][2]);
				int y2 = (int)(yu + fy * hTemplate.whiteAreas[i][3]);

				whiteAreaSum += ii[y1][x1] + ii[y2][x2] - ii[y1][x2] - ii[y2][x1];
			}
			const double blackAreaSum = ii[yu][xl] + ii[yd][xr] - ii[yu][xr] - ii[yd][xl] - whiteAreaSum;

			features[id] = whiteAreaSum / (hTemplate.whiteAreasField * fxfy) - blackAreaSum / (hTemplate.blackAreasField * fxfy);
		}
		return featuresCount;
	}

	/// <summary>Ekstrachuje cechy z podanego obrazu</summary>
	/// <param name = 'Wx'>Szerokosc okna</param>
	/// <param name = 'Wy'>Wysokosc okna</param>
	/// <param name = 'xp'>X-pozycja okna (lewy gorny rog)</param>
	/// <param name = 'yp'>Y-pozycja okna (lewy gorny rog)</param>
	/// <returns>Ekstrachowane cechy</returns>
	tuple<int, const double*> extractFromWindow(int Wx, int Wy, int xp = 0, int yp = 0) override
	{
		double* features = new double[featuresCount];

		int* fxiLocal = new int[scales + 1];
		int* fyiLocal = new int[scales + 1];
		double* hxiLocal = new double[scales + 1];
		double* hyiLocal = new double[scales + 1];

		for (int s = 1; s <= scales; s++)
		{
			fxiLocal[s] = (int)(Wx * powHSFT[s]);
			fyiLocal[s] = (int)(Wy * powHSFT[s]);

			hxiLocal[s] = (Wx - fxiLocal[s]) / (density - 1);
			hyiLocal[s] = (Wy - fyiLocal[s]) / (density - 1);
		}

		// przejscie po szablonach
		int id = 0;
		for (int t = 0; t < templatesCount; t++)
		{
			const HaarTemplate& hTemplate = HaarFeaturesShapes[t];
			// przejscie po skalach dla y
			for (int sx = 1; sx <= scales; sx++)
			{
				// przejscie po skalach dla x
				for (int sy = 1; sy <= scales; sy++)
				{
					int fxfy = fxiLocal[sx] * fyiLocal[sy];
					// przejscie po y
					for (int px = 0; px < density; px++)
					{
						// przejscie po x
						for (int py = 0; py < density; py++)
						{
							int xl = xp + (int)(px * hxiLocal[sx]);
							int xr = xl + fxiLocal[sx];
							int yu = yp + (int)(py * hyiLocal[sy]);
							int yd = yu + fyiLocal[sy];

							double whiteAreaSum = 0;
							for (int i = 0; i < (int)hTemplate.whiteAreas.size(); i++)
							{
								int x1 = (int)(xl + fxiLocal[sx] * hTemplate.whiteAreas[i][0]);
								int y1 = (int)(yu + fyiLocal[sy] * hTemplate.whiteAreas[i][1]);
								int x2 = (int)(xl + fxiLocal[sx] * hTemplate.whiteAreas[i][2]);
								int y2 = (int)(yu + fyiLocal[sy] * hTemplate.whiteAreas[i][3]);

								whiteAreaSum += ii[y1][x1] + ii[y2][x2] - ii[y1][x2] - ii[y2][x1];
							}
							const double blackAreaSum = ii[yu][xl] + ii[yd][xr] - ii[yu][xr] - ii[yd][xl] - whiteAreaSum;

							features[id] = (whiteAreaSum / (hTemplate.whiteAreasField * fxfy) - blackAreaSum / (hTemplate.blackAreasField * fxfy));
							id++;
						}
					}
				}
			}
		}

		delete[] fxiLocal;
		delete[] fyiLocal;
		delete[] hxiLocal;
		delete[] hyiLocal;

		return make_tuple(featuresCount, features);
	}

	/// <summary>Ekstrachuje cechy z podanych plikow</summary>
	/// <param name = 'paths'>Wektor sciezek do plikow</param>
	tuple<int, int, const double* const*> extractMultipleFeatures(const vector<string> &paths)  override
	{
		const double ** X = new const double*[paths.size()];

#pragma omp parallel for num_threads(OMP_NUM_THR)
		for (int i = 0; i < (int)paths.size(); i++)
		{
			HaarExtractor he(this);

			auto[fc, fets] = he.extractFeatures(paths[i]);
			X[i] = fets;
		}
		return make_tuple((int)paths.size(), featuresCount, X);
	}
};
const double HaarExtractor::halfOfSqrtFromTwo = sqrt(2.0) / 2.0;
const HaarExtractor::HaarTemplate HaarExtractor::HaarFeaturesShapes[HaarExtractor::tCount] =
{
	HaarExtractor::HaarTemplate{ { { 0.0, 0.0, 1.0, 0.5 } }, 0.5, 0.5 },
	HaarExtractor::HaarTemplate{ { { 0.0, 0.0, 0.5, 1.0 } }, 0.5, 0.5 },
	HaarExtractor::HaarTemplate{ { { 0.0, 0.0, 1.0, 1.0 / 3.0 },{ 0.0, 2.0 / 3.0, 1.0, 1.0 } }, 2.0 / 3.0, 1.0 - 2.0 / 3.0 },
	HaarExtractor::HaarTemplate{ { { 0.0, 0.0, 1.0 / 3.0, 1.0 },{ 2.0 / 3.0, 0.0, 1.0, 1.0 } }, 2.0 / 3.0, 1.0 - 2.0 / 3.0 },
	HaarExtractor::HaarTemplate{ { { 0.0, 0.0, 0.5, 0.5 },{ 0.5, 0.5, 1.0, 1.0 } }, 0.5, 0.5 },
	HaarExtractor::HaarTemplate{ { { 0.25, 0.25, 0.75, 0.75 } }, 0.25, 0.75 }
};

class HOGExtractor : public Extractor
{
private:
	int nx; /// <summary>Liczba roznych rozmiarów dla pojedynczej cechy</summary>
	int ny; /// <summary>Liczb molziwych polozen cechy w X oraz Y (liczba cech dla jednej skali = density ^ 2)</summary>
	int bins;
	const double eps = 0.000000001;
	const double epsSqr = pow(eps, 2);

	double*** ii = nullptr;
	double* angleBorders = nullptr;

	double*** Htmp = nullptr;
	double** sums = nullptr;

	struct FeatureDescriptor
	{
		int b;
		int nx;
		int ny;
	};

	FeatureDescriptor* descriptions = nullptr;

	double euclideanNorm(const double* array, int numElems)
	{
		double sum = 0;
		for (int i = 0; i < numElems; i++)
			sum += pow(array[i], 2);
		return sqrt(sum);
	}

	double sumOfSquares(const double* array, int numElems)
	{
		double sum = 0;
		for (int i = 0; i < numElems; i++)
			sum += pow(array[i], 2);
		return sum;
	}

	/// <summary>Wyznacza obraz ca³kowy dla podanego obrazu</summary>
	/// <param name = 'img'>Obraz</param>
	/// <returns>Obraz ca³kowy</returns>
	void calculateIntegralImage(const double* const* img)
	{
		ii = new double** [height + 1];
		for (int i = 0; i <= height; i++)
		{
			ii[i] = new double* [width + 1];
			for (int j = 0; j <= width; j++)
			{
				ii[i][j] = new double[bins];
			}
		}
		for (int i = 0; i <= height; i++)
			for (int b = 0; b < bins; b++)
				ii[i][0][b] = 0;
		for (int i = 0; i <= width; i++)
			for (int b = 0; b < bins; b++)
				ii[0][i][b] = 0;

		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				// pochonde do gradientu wzdluz x
				double dx;
				if (x == 0)
					dx = -img[y][0] + img[y][1];
				else if (x == width - 1)
					dx = -img[y][width - 2] + img[y][width - 1];
				else
					dx = -img[y][x - 1] + img[y][x + 1];

				// pochonde do gradientu wzdluz y
				double dy;
				if (y == 0)
					dy = -img[0][x] + img[1][x];
				else if (y == height - 1)
					dy = -img[height - 2][x] + img[height - 1][x];
				else
					dy = -img[y - 1][x] + img[y + 1][x];

				// si³a i kierunke gradientu
				double G = sqrt(pow(dx, 2) + pow(dy, 2));
				double dir = atan2(dy, dx);
				if (dir < 0)
					dir += M_PI * 2.0;

				// macierz g³osów
				for (int b = 0; b < bins; b++)
				{
					double a = 0;
					if (dir >= angleBorders[b] && dir < angleBorders[b + 1])
						ii[y + 1][x + 1][b] = ii[y][x + 1][b] + ii[y + 1][x][b] + G - ii[y][x][b];
					else
						ii[y + 1][x + 1][b] = ii[y][x + 1][b] + ii[y + 1][x][b] - ii[y][x][b];
				}
			}
		}
	}

	HOGExtractor(HOGExtractor* parent)
	{
		this->bins = parent->bins;
		this->nx = parent->nx;
		this->ny = parent->ny;
		this->saveMode = parent->saveMode;
		this->featuresCount = parent->featuresCount;

		Htmp = new double** [ny];
		sums = new double* [ny];
		for (int i = 0; i < ny; i++)
		{
			Htmp[i] = new double* [nx];
			sums[i] = new double[nx];
			for (int j = 0; j < nx; j++)
			{
				Htmp[i][j] = new double[bins];
			}
		}

		this->angleBorders = new double[bins + 1];
		for (int b = 0; b <= bins; b++)
			this->angleBorders[b] = parent->angleBorders[b];

		this->descriptions = new FeatureDescriptor[featuresCount];
		for (int i = 0; i < featuresCount; i++)
			this->descriptions[i] = parent->descriptions[i];
	}

public:
	using Extractor::extractFromWindow;

	~HOGExtractor()
	{
		if (ii != nullptr)
		{
			for (int i = 0; i <= height; i++)
			{
				for (int j = 0; j <= width; j++)
					delete[] ii[i][j];
				delete[] ii[i];
			}
			delete[] ii;
		}
		if (sums != nullptr)
		{
			for (int i = 0; i < ny; i++)
			{
				for (int j = 0; j < nx; j++)
				{
					delete[] Htmp[i][j];
				}
				delete[] Htmp[i];
				delete[] sums[i];
			}
			delete[] sums;
			delete[] Htmp;
		}
		delete[] descriptions;
		delete[] angleBorders;
	}

	HOGExtractor() : HOGExtractor(8, 5, 5, SaveFileType::text) {}

	/// <param name = 'scales'>Liczba roznych rozmiarów dla pojedynczej cech</param>
	/// <param name = 'density'>Liczb molziwych polozen cechy w X oraz Y (liczba cech dla jednej skali = density ^ 2)</param>
	HOGExtractor(int bins, int nx, int ny, SaveFileType fileType = SaveFileType::text)
	{
		this->bins = bins;
		this->nx = nx;
		this->ny = ny;
		this->saveMode = fileType;
		featuresCount = bins * nx * ny;

		Htmp = new double** [ny];
		sums = new double* [ny];
		for (int i = 0; i < ny; i++)
		{
			Htmp[i] = new double* [nx];
			sums[i] = new double[nx];
			for (int j = 0; j < nx; j++)
			{
				Htmp[i][j] = new double[bins];
			}
		}

		angleBorders = new double[bins + 1];
		for (int b = 0; b <= bins; b++)
			angleBorders[b] = -M_PI / bins + b * 2.0 * M_PI / bins;

		descriptions = new FeatureDescriptor[featuresCount];
		int f = 0;
		for (int i = 0; i < ny; i++)
		{
			for (int j = 0; j < nx; j++)
			{
				for (int b = 0; b < bins; b++)
				{
					descriptions[f] = FeatureDescriptor{ b, j, i };
					f++;
				}
			}
		}
	}

	static string GetType()
	{
		return "HOGExtractor";
	}

	/// <summary>Zwraca typ cechy</summary>
	/// <returns>Typ klasyfikatora</returns>
	string getType() const override
	{
		return GetType();
	}

	bool getRectangleWindowsRequirement() const override
	{
		return false;
	}

	/// <summary>Zwraca opis ekstraktora cech</summary>
	/// <returns>Opis ekstraktora cech</returns>
	string toString() const override
	{
		string text = getType() + "\r\n";
		text += "Binns: " + to_string(bins) + "\r\n";
		text += "Blocks (X): " + to_string(nx) + "\r\n";
		text += "Blocks (Y): " + to_string(ny) + "\r\n";

		return text;
	}

	void loadImageData(const string path) override
	{
		if (ii != nullptr)
		{
			for (int i = 0; i <= height; i++)
			{
				for (int j = 0; j <= width; j++)
					delete[] ii[i][j];
				delete[] ii[i];
			}
			delete[] ii;
			ii = nullptr;
		}

		auto [height, width, img] = loadImage(path, saveMode);
		this->width = width;
		this->height = height;

		calculateIntegralImage(img);

		for (int i = 0; i < height; i++)
			delete[] img[i];
		delete[] img;
	}

	void loadImageData(const double* const* img, int height, int width) override
	{
		if (ii != nullptr)
		{
			for (int i = 0; i <= height; i++)
			{
				for (int j = 0; j <= width; j++)
					delete[] ii[i][j];
				delete[] ii[i];
			}
			delete[] ii;
			ii = nullptr;
		}

		this->width = width;
		this->height = height;

		calculateIntegralImage(img);
	}

	void clearImageData() override
	{
		if (ii != nullptr)
		{
			for (int i = 0; i <= height; i++)
			{
				for (int j = 0; j <= width; j++)
					delete[] ii[i][j];
				delete[] ii[i];
			}
			delete[] ii;
			ii = nullptr;
		}
	}

	void initializeExtractor(Point * sizes, int sca) override
	{
	}

	/// <param name = 'featuresID'>Numery cech</param>
	/// <param name = 'Wx'>Szerokosc okna</param>
	/// <param name = 'Wy'>Wysokosc okna</param>
	/// <param name = 'xp'>X-pozycja okna (lewy gorny rog)</param>
	/// <param name = 'yp'>Y-pozycja okna (lewy gorny rog)</param>
	//int extractFromWindow(double* features, const int* featuresID, int fLength, int Wx, int Wy, int xp = 0, int yp = 0) override
	//{
	//	double*** Htmp = new double** [ny];
	//	for (int i = 0; i < ny; i++)
	//	{
	//		Htmp[i] = new double* [nx];
	//		for (int j = 0; j < nx; j++)
	//			Htmp[i][j] = new double[bins];
	//	}
//
	//	double dx = 1.0 * Wx / nx;
	//	double dy = 1.0 * Wy / ny;
//
	//	for (int i = 0; i < ny; i++)
	//	{
	//		int yStart = yp + (int)floor(i * dy);
	//		int yEnd = yp + (int)floor((i + 1.0) * dy);
	//		for (int j = 0; j < nx; j++)
	//		{
	//			int xStart = xp + (int)floor(j * dx);
	//			int xEnd = xp + (int)floor((j + 1.0) * dx);
	//			for (int b = 0; b < bins; b++)
	//			{
	//				Htmp[i][j][b] = ii[yStart][xStart][b] + ii[yEnd][xEnd][b] - ii[yStart][xEnd][b] - ii[yEnd][xStart][b];
	//			}
	//		}
	//	}
//
	//	for (int f = 0; f < fLength; f++)
	//	{
	//		int id = featuresID[f];
//
	//		int& i = descriptions[id].ny;
	//		int& j = descriptions[id].nx;
	//		int& b = descriptions[id].b;
//
	//		double sum = sqrt(sumOfSquares(Htmp[i][j], bins) + epsSqr);
	//		if (i > 0)
	//		{
	//			sum += sqrt(sumOfSquares(Htmp[i - 1][j], bins) + epsSqr);
	//			if (j > 0)
	//				sum += sqrt(sumOfSquares(Htmp[i - 1][j - 1], bins) + epsSqr);
	//			if (j < nx - 1)
	//				sum += sqrt(sumOfSquares(Htmp[i - 1][j + 1], bins) + epsSqr);
	//		}
	//		if (i < ny - 1)
	//		{
	//			sum += sqrt(sumOfSquares(Htmp[i + 1][j], bins) + epsSqr);
	//			if (j > 0)
	//				sum += sqrt(sumOfSquares(Htmp[i + 1][j - 1], bins) + epsSqr);
	//			if (j < nx - 1)
	//				sum += sqrt(sumOfSquares(Htmp[i + 1][j + 1], bins) + epsSqr);
	//		}
	//		if (j > 0)
	//			sum += sqrt(sumOfSquares(Htmp[i][j - 1], bins) + epsSqr);
	//		if (j < nx - 1)
	//			sum += sqrt(sumOfSquares(Htmp[i][j + 1], bins) + epsSqr);
//
	//		features[id] = Htmp[i][j][b] / sum;
	//	}
//
	//	for (int i = 0; i < ny; i++)
	//	{
	//		for (int j = 0; j < nx; j++)
	//			delete[] Htmp[i][j];
	//		delete[] Htmp[i];
	//	}
	//	delete[] Htmp;
//
	//	return featuresCount;
	//}
//
	//int extractFromWindow(double* features, const int* featuresID, int fLength, int Wx, int Wy, int xp = 0, int yp = 0) override
	//{
	//	double*** Htmp = new double** [ny];
	//	double** sums = new double* [ny];
	//	for (int i = 0; i < ny; i++)
	//	{
	//		Htmp[i] = new double* [nx];
	//		sums[i] = new double [nx];
	//		for (int j = 0; j < nx; j++)
	//			Htmp[i][j] = new double[bins];
	//	}
//
	//	double dx = 1.0 * Wx / nx;
	//	double dy = 1.0 * Wy / ny;
//
	//	for (int i = 0; i < ny; i++)
	//	{
	//		int yStart = yp + (int)floor(i * dy);
	//		int yEnd = yp + (int)floor((i + 1.0) * dy);
	//		for (int j = 0; j < nx; j++)
	//		{
	//			int xStart = xp + (int)floor(j * dx);
	//			int xEnd = xp + (int)floor((j + 1.0) * dx);
	//			sums[i][j] = epsSqr;
	//			for (int b = 0; b < bins; b++)
	//			{
	//				Htmp[i][j][b] = ii[yStart][xStart][b] + ii[yEnd][xEnd][b] - ii[yStart][xEnd][b] - ii[yEnd][xStart][b];
	//				sums[i][j] += pow(Htmp[i][j][b], 2);
	//			}
	//			sums[i][j] = sqrt(sums[i][j]);
	//		}
	//	}
//
	//	for (int f = 0; f < fLength; f++)
	//	{
	//		int id = featuresID[f];
//
	//		int& i = descriptions[id].ny;
	//		int& j = descriptions[id].nx;
	//		int& b = descriptions[id].b;
//
	//		double sum = 0.0;
	//		int iStart = (i > 0) ? i - 1 : 0;
	//		int iEnd = (i < ny - 1) ? i + 1 : ny - 1;
	//		int jStart = (j > 0) ? j - 1 : 0;
	//		int jEnd = (j < nx - 1) ? j + 1 : nx - 1;
	//		for (int k = iStart; k <= iEnd; k++)
	//			for (int l = jStart; l <= jEnd; l++)
	//				sum += sums[k][l];
//
	//		features[id] = Htmp[i][j][b] / sum;
	//	}
//
	//	for (int i = 0; i < ny; i++)
	//	{
	//		for (int j = 0; j < nx; j++)
	//			delete[] Htmp[i][j];
	//		delete[] Htmp[i];
	//		delete[] sums[i];
	//	}
	//	delete[] Htmp;
	//	delete[] sums;
//
	//	return featuresCount;
	//}

	int currX = -1000;
	int currY = -1000;
	int extractFromWindow(double* features, const int* featuresID, int fLength, int Wx, int Wy, int xp = 0, int yp = 0) override
	{
		if (xp != currX || yp != currY)
		{
			currX = xp;
			currY = yp;

			for (int i = 0; i < ny; i++)
			{
				for (int j = 0; j < nx; j++)
				{
					sums[i][j] = 0.0;
				}
			}
		}

		double dx = 1.0 * Wx / nx;
		double dy = 1.0 * Wy / ny;

		for (int f = 0; f < fLength; f++)
		{
			int id = featuresID[f];

			int& i = descriptions[id].ny;
			int& j = descriptions[id].nx;
			int& b = descriptions[id].b;

			double sum = 0.0;
			int iStart = (i > 0) ? i - 1 : 0;
			int iEnd = (i < ny - 1) ? i + 1 : ny - 1;
			int jStart = (j > 0) ? j - 1 : 0;
			int jEnd = (j < nx - 1) ? j + 1 : nx - 1;

			for (int k = iStart; k <= iEnd; k++)
				for (int l = jStart; l <= jEnd; l++)
					if (sums[k][l] == 0.0)
					{
						int yStart = yp + (int)floor(k * dy);
						int yEnd = yp + (int)floor((k + 1.0) * dy);
						int xStart = xp + (int)floor(l * dx);
						int xEnd = xp + (int)floor((l + 1.0) * dx);

						for (int b = 0; b < bins; b++)
						{
							Htmp[k][l][b] = ii[yStart][xStart][b] + ii[yEnd][xEnd][b] - ii[yStart][xEnd][b] - ii[yEnd][xStart][b];
							sums[k][l] += pow(Htmp[k][l][b], 2);
						}
						sums[k][l] = sqrt(sums[k][l] + epsSqr);
						sum += sums[k][l];
					}
					else
						sum += sums[k][l];

			features[id] = Htmp[i][j][b] / sum;
		}

		return featuresCount;
	}

	//int extractFromWindow(double* features, const int* featuresID, int fLength, int Wx, int Wy, int xp = 0, int yp = 0) override
	//{
	//	double*** Htmp = new double** [ny];
	//	double** sums = new double* [ny];
	//	for (int i = 0; i < ny; i++)
	//	{
	//		Htmp[i] = new double* [nx];
	//		sums[i] = new double[nx];
	//		for (int j = 0; j < nx; j++)
	//		{
	//			Htmp[i][j] = new double[bins];
	//			sums[i][j] = 0.0;
	//		}
	//	}

	//	double dx = 1.0 * Wx / nx;
	//	double dy = 1.0 * Wy / ny;

	//	for (int f = 0; f < fLength; f++)
	//	{
	//		int id = featuresID[f];

	//		int& i = descriptions[id].ny;
	//		int& j = descriptions[id].nx;
	//		int& b = descriptions[id].b;

	//		double sum = 0.0;
	//		int iStart = (i > 0) ? i - 1 : 0;
	//		int iEnd = (i < ny - 1) ? i + 1 : ny - 1;
	//		int jStart = (j > 0) ? j - 1 : 0;
	//		int jEnd = (j < nx - 1) ? j + 1 : nx - 1;

	//		for (int k = iStart; k <= iEnd; k++)
	//			for (int l = jStart; l <= jEnd; l++)
	//				if (sums[k][l] == 0.0)
	//				{
	//					int yStart = yp + (int)floor(k * dy);
	//					int yEnd = yp + (int)floor((k + 1.0) * dy);
	//					int xStart = xp + (int)floor(l * dx);
	//					int xEnd = xp + (int)floor((l + 1.0) * dx);

	//					for (int b = 0; b < bins; b++)
	//					{
	//						Htmp[k][l][b] = ii[yStart][xStart][b] + ii[yEnd][xEnd][b] - ii[yStart][xEnd][b] - ii[yEnd][xStart][b];
	//						sums[k][l] += pow(Htmp[k][l][b], 2);
	//					}
	//					sums[k][l] = sqrt(sums[k][l] + epsSqr);
	//					sum += sums[k][l];
	//				}
	//				else
	//					sum += sums[k][l];

	//		features[id] = Htmp[i][j][b] / sum;
	//	}

	//	for (int i = 0; i < ny; i++)
	//	{
	//		for (int j = 0; j < nx; j++)
	//			delete[] Htmp[i][j];
	//		delete[] Htmp[i];
	//		delete[] sums[i];
	//	}
	//	delete[] Htmp;
	//	delete[] sums;

	//	return featuresCount;
	//}

	/// <summary>Ekstrachuje cechy z podanego obrazu</summary>
	/// <param name = 'Wx'>Szerokosc okna</param>
	/// <param name = 'Wy'>Wysokosc okna</param>
	/// <param name = 'xp'>X-pozycja okna (lewy gorny rog)</param>
	/// <param name = 'yp'>Y-pozycja okna (lewy gorny rog)</param>
	/// <returns>Ekstrachowane cechy</returns>
	tuple<int, const double*> extractFromWindow(int Wx, int Wy, int xp = 0, int yp = 0) override
	{
		double* features = new double[featuresCount];

		double*** Htmp = new double** [ny];
		for (int i = 0; i < ny; i++)
		{
			Htmp[i] = new double* [nx];
			for (int j = 0; j < nx; j++)
				Htmp[i][j] = new double[bins];
		}

		double dx = 1.0 * Wx / nx;
		double dy = 1.0 * Wy / ny;

		for (int i = 0; i < ny; i++)
		{
			int yStart = yp + (int)floor(i * dy);
			int yEnd = yp + (int)floor((i + 1.0) * dy);
			for (int j = 0; j < nx; j++)
			{
				int xStart = xp + (int)floor(j * dx);
				int xEnd = xp + (int)floor((j + 1.0) * dx);
				for (int b = 0; b < bins; b++)
					Htmp[i][j][b] = ii[yStart][xStart][b] + ii[yEnd][xEnd][b] - ii[yStart][xEnd][b] - ii[yEnd][xStart][b];
			}
		}

		int id = 0;
		for (int i = 0; i < ny; i++)
		{
			for (int j = 0; j < nx; j++)
			{
				//double sum = sumOfSquares(Htmp[i][j], bins);
				//if (i > 0)
				//{
				//	sum += sumOfSquares(Htmp[i - 1][j], bins);
				//	if (j > 0)
				//		sum += sumOfSquares(Htmp[i - 1][j - 1], bins);
				//	if (j < nx - 1)
				//		sum += sumOfSquares(Htmp[i - 1][j + 1], bins);
				//}
				//if (i < ny - 1)
				//{
				//	sum += sumOfSquares(Htmp[i + 1][j], bins);
				//	if (j > 0)
				//		sum += sumOfSquares(Htmp[i + 1][j - 1], bins);
				//	if (j < nx - 1)
				//		sum += sumOfSquares(Htmp[i + 1][j + 1], bins);
				//}
				//if (j > 0)
				//	sum += sumOfSquares(Htmp[i][j - 1], bins);
				//if (j < nx - 1)
				//	sum += sumOfSquares(Htmp[i][j + 1], bins);
				//sum = sqrt(pow(sum, 2) + epsSqr);
			
				double sum = sqrt(pow(euclideanNorm(Htmp[i][j], bins), 2) + epsSqr);
				if (i > 0)
				{
					sum += sqrt(pow(euclideanNorm(Htmp[i - 1][j], bins), 2) + epsSqr);
					if (j > 0)
						sum += sqrt(pow(euclideanNorm(Htmp[i - 1][j - 1], bins), 2) + epsSqr);
					if (j < nx - 1)
						sum += sqrt(pow(euclideanNorm(Htmp[i - 1][j + 1], bins), 2) + epsSqr);
				}
				if (i < ny - 1)
				{
					sum += sqrt(pow(euclideanNorm(Htmp[i + 1][j], bins), 2) + epsSqr);
					if (j > 0)
						sum += sqrt(pow(euclideanNorm(Htmp[i + 1][j - 1], bins), 2) + epsSqr);
					if (j < nx - 1)
						sum += sqrt(pow(euclideanNorm(Htmp[i + 1][j + 1], bins), 2) + epsSqr);
				}
				if (j > 0)
					sum += sqrt(pow(euclideanNorm(Htmp[i][j - 1], bins), 2) + epsSqr);
				if (j < nx - 1)
					sum += sqrt(pow(euclideanNorm(Htmp[i][j + 1], bins), 2) + epsSqr);

				for (int b = 0; b < bins; b++)
				{
					features[id] = Htmp[i][j][b] / sum;
					id++;
				}
			}
		}

		for (int i = 0; i < ny; i++)
		{
			for (int j = 0; j < nx; j++)
				delete[] Htmp[i][j];
			delete[] Htmp[i];
		}
		delete[] Htmp;

		return make_tuple(featuresCount, features);
	}

	/// <summary>Ekstrachuje cechy z podanych plikow</summary>
	/// <param name = 'paths'>Wektor sciezek do plikow</param>
	tuple<int, int, const double* const*> extractMultipleFeatures(const vector<string> & paths)  override
	{
		const double** X = new const double* [paths.size()];

#pragma omp parallel for num_threads(OMP_NUM_THR)
		for (int i = 0; i < (int)paths.size(); i++)
		{
			HOGExtractor hog(this);

			auto [fc, fets] = hog.extractFeatures(paths[i]);
			X[i] = fets;
		}
		return make_tuple((int)paths.size(), featuresCount, X);
	}
};

class PFMM : public Extractor
{
private:
	vector<vector<vector<vector<complex<double>>>>> Integrals;

	static bool initialized;
	static constexpr double pi2 = 2 * M_PI;

	static vector<int> sOdd_t;
	static vector<int> q_n_t;
	static vector<int> sMin_t;

	static vector<vector<vector<vector<complex<double>>>>> Speeder;
	static vector<vector<unsigned long long int>> bc;
	static vector<vector<long long>> Alpha;
	static vector<double> a;

	static void initializeAlphas()
	{
		string fileName = "alphas.bin";

		ifstream file(fileName, std::ios_base::binary);
		if (file.good())
		{
			int pSize;
			file.read(reinterpret_cast<char*>(&pSize), sizeof(pSize));
			if (pSize != pLimit)
			{
				file.close();
				remove(fileName.c_str());
			}
		}

		if (file.is_open() && file.good())
		{
			for (int n = 0; n <= pLimit; n++)
			{
				Alpha[n].resize(n + 1);
				file.read(reinterpret_cast<char*>(Alpha[n].data()), sizeof(long long) * (n + 1));
			}
			file.read(reinterpret_cast<char*>(a.data()), sizeof(double) * (pLimit + 1));
		}
		else
		{
			for (int n = 0; n <= pLimit; n++)
			{
				Alpha[n].resize(n + 1);
				for (int s = 0; s <= n; s++)
					Alpha[n][s] = simplificationQ(n, s);
				a[n] = (1.0 / (2 * (n + 1)));
			}

			ofstream file(fileName, std::ios_base::binary);

			file.write(reinterpret_cast<const char*>(&pLimit), sizeof(pLimit));
			for (int n = 0; n <= pLimit; n++)
				file.write(reinterpret_cast<const char*>(Alpha[n].data()), sizeof(long long) * (n + 1));
			file.write(reinterpret_cast<const char*>(a.data()), sizeof(double) * (pLimit + 1));
			file.close();
		}
		if (file.is_open())
			file.close();
	}

	static void initialzeBinomials()
	{
		bc.clear();

		string fileName = "binomials.bin";
		int n = pLimit + 2 * qLimit + 1;

		ifstream file(fileName, std::ios_base::binary);
		if (file.good())
		{
			int qSize, pSize;
			file.read(reinterpret_cast<char*>(&qSize), sizeof(qSize));
			file.read(reinterpret_cast<char*>(&pSize), sizeof(pSize));
			if (pSize != pLimit || qSize != qLimit)
			{
				file.close();
				remove(fileName.c_str());
			}
		}

		if (file.is_open() && file.good())
		{
			bc.resize(n);
			for (int i = 0; i < n; i++)
			{
				bc[i].resize(i + 1);
				file.read(reinterpret_cast<char*>(bc[i].data()), sizeof(unsigned long long) * (i + 1));
			}
		}
		else
		{
			binomial(pLimit, qLimit, bc);

			ofstream file(fileName, std::ios_base::binary);
			file.write(reinterpret_cast<const char*>(&qLimit), sizeof(qLimit));
			file.write(reinterpret_cast<const char*>(&pLimit), sizeof(pLimit));
			for (int i = 0; i < n; i++)
				file.write(reinterpret_cast<const char*>(bc[i].data()), sizeof(unsigned long long) * (i + 1));
			file.close();
		}
		if (file.is_open())
			file.close();
	}

	static void initialzeSpeeder()
	{
		Speeder.clear();

		string fileName = "speeders.bin";

		int maxExponent = (int)ceil(pLimit / 2.0 + qLimit / 2.0);

		ifstream file(fileName, std::ios_base::binary);
		if (file.good())
		{
			int wSize, qSize, pSize;
			file.read(reinterpret_cast<char*>(&wSize), sizeof(wSize));
			file.read(reinterpret_cast<char*>(&qSize), sizeof(qSize));
			file.read(reinterpret_cast<char*>(&pSize), sizeof(pSize));
			if (pSize != pLimit || qSize != qLimit || wSize != predictedMaxWindowSize)
			{
				file.close();
				remove(fileName.c_str());
			}
		}

		if (file.is_open() && file.good())
		{
			Speeder.resize(predictedMaxWindowSize);
			for (int i = 0; i < predictedMaxWindowSize; i++)
			{
				Speeder[i].resize(predictedMaxWindowSize);
				for (int j = 0; j < predictedMaxWindowSize; j++)
				{
					Speeder[i][j].resize(maxExponent + 1);
					for (int expT = 0; expT <= maxExponent; expT++)
					{
						Speeder[i][j][expT].resize(maxExponent + 2);
						file.read(reinterpret_cast<char*>(Speeder[i][j][expT].data()), sizeof(complex<double>) * (maxExponent + 2));
					}
				}
			}
		}
		else
		{
			speeder(predictedMaxWindowSize);

			ofstream file(fileName, std::ios_base::binary);
			file.write(reinterpret_cast<const char*>(&predictedMaxWindowSize), sizeof(predictedMaxWindowSize));
			file.write(reinterpret_cast<const char*>(&qLimit), sizeof(qLimit));
			file.write(reinterpret_cast<const char*>(&pLimit), sizeof(pLimit));
			for (int i = 0; i < predictedMaxWindowSize; i++)
				for (int j = 0; j < predictedMaxWindowSize; j++)
					for (int expT = 0; expT <= maxExponent; expT++)
						file.write(reinterpret_cast<const char*>(Speeder[i][j][expT].data()), sizeof(complex<double>) * (maxExponent + 2));
			file.close();
		}
		if (file.is_open())
			file.close();
	}

	static int simplificationQ(int n, int s)
	{
		if (s == 0 && n == 0)
			return 1;
		else if (s == 0)
			return (int)(pow(-1, n)*(n + 1));

		vector<int> licznik((n + s + 1) - (n - s + 1) + 1);
		iota(licznik.begin(), licznik.end(), n - s + 1);

		vector<int> mianownik(s);
		for (int i = 2; i <= s; i++)
			mianownik[i - 2] = (int)pow(i, 2);
		mianownik[s - 1] = s + 1;

		vector<int> ind_licznik(licznik.size());
		fill(ind_licznik.begin(), ind_licznik.end(), 1);

		vector<int> ind_mianownik(mianownik.size());
		fill(ind_mianownik.begin(), ind_mianownik.end(), 1);

		while (true)
		{
			for (int i = 0; i < (int)mianownik.size(); i++)
			{
				for (int j = 0; j < (int)licznik.size(); j++)
				{
					int a = gcd(licznik[j], mianownik[i]);
					if (a > 1)
					{
						licznik[j] /= a;
						mianownik[i] /= a;
						break;
					}
				}
			}
			vector<int> res_licznik;
			vector<int> res_mianownik;
			for (int i = 0; i < (int)mianownik.size(); i++)
				if (mianownik[i] != 1)
					res_mianownik.push_back(mianownik[i]);
			for (int i = 0; i < (int)licznik.size(); i++)
				if (licznik[i] != 1)
					res_licznik.push_back(licznik[i]);
			if (licznik.size() == res_licznik.size() && mianownik.size() == res_mianownik.size())
				break;
			mianownik = res_mianownik;
			licznik = res_licznik;
		}

		mianownik.push_back(1);
		return (int)(pow(-1, n + s)*prod(licznik) / prod(mianownik));
	}

	static void speeder(int maxsize)
	{
		Speeder.clear();

		int s1 = maxsize;
		Speeder.resize(s1);
		int maxExponent = (int)ceil(pLimit / 2.0 + qLimit / 2.0);
		for (int j = 0; j < s1; j++)
		{
			double jc = j + 0.5;
			Speeder[j] = vector<vector<vector<complex<double>>>>(s1);
			for (int k = 0; k < s1; k++)
			{
				double kc = k + 0.5;
				Speeder[j][k] = vector<vector<complex<double>>>(maxExponent + 1);
				for (int expT = 0; expT <= maxExponent; expT++)
				{
					Speeder[j][k][expT] = vector<complex<double>>(maxExponent + 2);
					complex<double> factorT = pow(complex<double>(-kc, jc), expT);
					for (int expU = 0; expU <= maxExponent + 1; expU++)
					{
						complex<double> factorU = pow(complex<double>(-kc, -jc), expU);
						Speeder[j][k][expT][expU] = factorT * factorU;
					}
				}
			}
		}
	}

	static void speederUpdate(int maxsize)
	{
		int minsize = (int)Speeder.size();

		int s1 = maxsize;
		int maxExponent = (int)ceil(pLimit / 2.0 + qLimit / 2.0);

		// doliczenie nowych kolumn dla istniejacych wierszy
		for (int j = 0; j < minsize; j++)
		{
			double jc = j + 0.5;
			Speeder[j].resize(s1);
			for (int k = minsize; k < s1; k++)
			{
				double kc = k + 0.5;
				Speeder[j][k] = vector<vector<complex<double>>>(maxExponent + 1);
				for (int expT = 0; expT <= maxExponent; expT++)
				{
					Speeder[j][k][expT] = vector<complex<double>>(maxExponent + 2);
					complex<double> factorT = pow(complex<double>(-kc, jc), expT);
					for (int expU = 0; expU <= maxExponent + 1; expU++)
					{
						complex<double> factorU = pow(complex<double>(-kc, -jc), expU);
						Speeder[j][k][expT][expU] = factorT * factorU;
					}
				}
			}
		}

		// doliczenie nowych wierszy
		Speeder.resize(s1);
		for (int j = minsize; j < s1; j++)
		{
			double jc = j + 0.5;
			Speeder[j] = vector<vector<vector<complex<double>>>>(s1);
			for (int k = 0; k < s1; k++)
			{
				double kc = k + 0.5;
				Speeder[j][k] = vector<vector<complex<double>>>(maxExponent + 1);
				for (int expT = 0; expT <= maxExponent; expT++)
				{
					Speeder[j][k][expT] = vector<complex<double>>(maxExponent + 2);
					complex<double> factorT = pow(complex<double>(-kc, jc), expT);
					for (int expU = 0; expU <= maxExponent + 1; expU++)
					{
						complex<double> factorU = pow(complex<double>(-kc, -jc), expU);
						Speeder[j][k][expT][expU] = factorT * factorU;
					}
				}
			}
		}
	}

	void calculateII2(const double* const* &img)
	{
		Integrals.clear();

		int s1 = width;
		int s2 = height;

		Integrals.reserve(p_max + q_max);
		for (int t = 0; t <= p_max + q_max; t++)
		{
			Integrals.push_back(vector<vector<vector<complex<double>>>>());
		}
		for (int q = 0; q <= q_max; q++)
		{
			for (int p = 0; p <= p_max; p++)
			{
				int sOdd = abs(q) % 2;
				int q_nn = (int)round((q - sOdd) / 2.0);
				int sMin = (int)ceil((abs(q) - sOdd) / 2.0);
				int sMax = (int)floor((p - sOdd) / 2.0);
				for (int s = sMin; s <= sMax; s++)
				{
					for (int t = 0; t <= s - q_nn; t++)
					{
						Integrals[t].reserve(s + q_nn + sOdd);
						for (int u = 0; u <= s + q_nn + sOdd; u++)
						{
							Integrals[t].push_back(vector<vector<complex<double>>>());
							if (u <= p_max + q_max && t <= p_max + q_max && Integrals[t][u].size() == 0)
							{
								// cout << t << " " << u << endl;
								vector<complex<double>> ll(s1);
								Integrals[t][u].reserve(s2);
								for (int y = 0; y < s2; y++)
								{
									Integrals[t][u].push_back(vector<complex<double>>());
									Integrals[t][u][y].reserve(s1);
									for (int x = 0; x < s1; x++)
									{
										Integrals[t][u][y].push_back(complex<double>());
										if (t <= u)
										{
											complex<double> a = img[y][x] * (pow(complex<double>(x + 1, -(y + 1)), t)
												*pow(complex<double>(x + 1, y + 1), u));
											complex<double> s;
											if (x > 0)
												s = ll[x - 1] + a;
											else
												s = a;
											ll[x] = s;
											if (y > 0)
												s = s + Integrals[t][u][y - 1][x];
											Integrals[t][u][y][x] = s;
										}
										else
											Integrals[t][u][y][x] = conj(Integrals[u][t][y][x]);
									}
								}
								ll.clear();
							}
						}
					}
				}
			}
		}
	}

	complex<double> deltaII(const vector<vector<complex<double>>> &Integral, const int jp, const int kp, const int N) const
	{
		complex<double> delta;
		if (jp == 0 && kp == 0)
			delta = Integral[jp + N - 1][kp + N - 1];
		else if (jp == 0)
			delta = Integral[jp + N - 1][kp + N - 1] - Integral[jp + N - 1][kp - 1];
		else if (kp == 0)
			delta = Integral[jp + N - 1][kp + N - 1] - Integral[jp - 1][kp + N - 1];
		else
			delta = Integral[jp + N - 1][kp + N - 1] - Integral[jp + N - 1][kp - 1]
			- Integral[jp - 1][kp + N - 1] + Integral[jp - 1][kp - 1];
		return delta;
	}

	struct FeatureDescriptor
	{
		int r;
		int rt;
		int p;
		int q;
	};

	vector<FeatureDescriptor> descriptions;

	const int p_max;
	const int q_max;
	const int rings;
	const int ringsType;

	map<int, complex<double>*> M_p_w;

	PFMM(PFMM * parent) : p_max{ parent->p_max }, q_max{ parent->q_max }, rings{ parent->rings }, ringsType{ parent->ringsType }
	{
		this->saveMode = parent->saveMode;

		for (map<int, complex<double>*>::iterator iter = M_p_w.begin(); iter != M_p_w.end(); ++iter)
		{
			int k = iter->first;

			this->M_p_w[k] = new complex<double>[p_max + 1];
			for (int p = 0; p <= p_max; p++)
				this->M_p_w[k][p] = parent->M_p_w[k][p];
		}

		this->descriptions = parent->descriptions;
		this->featuresCount = parent->featuresCount;
	}
public:
	using Extractor::extractFromWindow;

	static void initializeExtractor()
	{
		if (!initialized)
		{
			sOdd_t.resize(PFMM::qLimit + 1);
			q_n_t.resize(PFMM::qLimit + 1);
			sMin_t.resize(PFMM::qLimit + 1);
			Alpha.resize(PFMM::pLimit + 1);
			a.resize(PFMM::pLimit + 1);

			for (int q = 0; q <= qLimit; q++)
			{
				sOdd_t[q] = abs(q) % 2;
				q_n_t[q] = (int)round((q - sOdd_t[q]) / 2.0);
				sMin_t[q] = (int)ceil((abs(q) - sOdd_t[q]) / 2.0);
			}

			initializeAlphas();
			initialzeBinomials();
			initialzeSpeeder();

			initialized = true;
		}
	}

	static void clearMemory()
	{
		sMin_t = vector<int>();
		sOdd_t = vector<int>();
		q_n_t = vector<int>();

		Speeder = vector<vector<vector<vector<complex<double>>>>>();
		bc = vector<vector<unsigned long long int>>();
		Alpha = vector<vector<long long>>();
		a = vector<double>();
		initialized = false;
	}

	~PFMM()
	{
		Integrals.clear();

		for (map<int, complex<double>*>::iterator iter = M_p_w.begin(); iter != M_p_w.end(); ++iter)
			delete[] M_p_w[iter->first];
		M_p_w.clear();
	}

	PFMM() : PFMM(8, 8, 6, 0, SaveFileType::text) {}

	PFMM(const int p_max, const int q_max, const int rings, const int ringsType, SaveFileType fileType = SaveFileType::text) 
		: p_max{ p_max }, q_max{ q_max }, rings{ rings }, ringsType{ ringsType }
	{
		if (!initialized)
			initializeExtractor();

		this->saveMode = fileType;

		for (int r = 0; r < rings; r++)
		{
			int hMax = 0;
			if (ringsType == 1)
				hMax = (r == rings - 1) ? 0 : 1;

			for (int rt = 0; rt <= hMax; rt++)
			{
				for (int p = 0; p <= p_max; p++)
				{
					for (int q = 0; q <= min(p, q_max); q++)
					{
						descriptions.push_back(FeatureDescriptor{ r, rt, p, q });
					}
				}
			}
		}
		featuresCount = (int)descriptions.size();
	}

	static string GetType()
	{
		return "PFMMExtractor";
	}

	/// <summary>Zwraca typ cechy</summary>
	/// <returns>Typ klasyfikatora</returns>
	string getType() const override
	{
		return GetType();
	}

	bool getRectangleWindowsRequirement() const override
	{
		return true;
	}

	/// <summary>Zwraca opis ekstraktora cech</summary>
	/// <returns>Opis ekstraktora cech</returns>
	string toString() const override
	{
		string text = getType() + "\r\n";
		text += "Pmax: " + to_string(p_max) + "\r\n";
		text += "Qmax: " + to_string(q_max) + "\r\n";
		text += "Rings: " + to_string(rings) + "\r\n";
		text += "RingsType: " + to_string(ringsType) + "\r\n";

		return text;
	}

	void loadImageData(const string path)
	{
		auto[height, width, img] = loadImage(path, saveMode);
		this->width = width;
		this->height = height;

		int maxDim = max(width, height);

		calculateII2(img);

#pragma omp critical (RecalculateSpeeder)
		{
			if ((int)Speeder.size() < maxDim)
				speederUpdate(width);
		}

		for (int i = 0; i < height; i++)
			delete[] img[i];
		delete[] img;
	}

	void loadImageData(const double* const* img, int height, int width) override
	{
		this->width = width;
		this->height = height;
		int maxDim = max(width, height);

		calculateII2(img);

#pragma omp critical (RecalculateSpeeder)
		{
			if ((int)Speeder.size() < maxDim)
				speederUpdate(maxDim);
		}
	}

	void clearImageData() override
	{
		Integrals.clear();
	}

	void initializeExtractor(Point* sizes, int sca) override
	{
		if (!initialized)
			initializeExtractor();

		for (int s1 = 0; s1 < sca; s1++)
		{
			int Wx = sizes[s1].wx;

			if (M_p_w.count(Wx) == 0)
			{
				this->M_p_w[Wx] = new complex<double>[p_max + 1];
				for (int p = 0; p <= p_max; p++)
					this->M_p_w[Wx][p] = (2.0 / pow(Wx, 2)) / (pi2 * a[p]);
			}
		}
	}

	/// <param name = 'featuresID'>Numery cech</param>
	/// <param name = 'Wx'>Szerokosc okna</param>
	/// <param name = 'Wy'>Wysokosc okna</param>
	/// <param name = 'xp'>X-pozycja okna (lewy gorny rog)</param>
	/// <param name = 'yp'>Y-pozycja okna (lewy gorny rog)</param>
	virtual int extractFromWindow(double* features, const int* featuresID, int fLength, int Wx, int Wy, int kp = 0, int jp = 0) override
	{
		complex<double>* M_p = M_p_w[Wx];

		//return extractFromWindow(Wx, Wy, xp, yp);
		double jc = jp + 1 + (Wx - 1) / 2.0;
		double kc = kp + 1 + (Wx - 1) / 2.0;
		int N = Wx;

		vector<vector<vector<vector<complex<double>>>>> deltasSpeeder(ringsType + 1,
			vector<vector<vector<complex<double>>>>(rings,
				vector<vector<complex<double>>>((p_max + q_max + 2) / 2,
					vector<complex<double>>((p_max + q_max + 2) / 2))));

		int jc2 = (int)round(jc - 0.5);
		int kc2 = (int)round(kc - 0.5);

		vector<int> x1I(rings), y1I(rings), x2I(rings), y2I(rings);
		vector<int> x1_t(rings), y1_t(rings), x2_t(rings), y2_t(rings);
		for (int r = 0; r < rings; r++)
		{
			int WInner = (int)round(N * sqrt((rings - (r + 1)) / (double)rings));
			if (WInner % 2 == 1)
				WInner++;
			x1I[r] = (int)(jc - 0.5 - WInner / 2);
			y1I[r] = (int)(kc - 0.5 - WInner / 2);
			x2I[r] = x1I[r] + WInner - 1;
			y2I[r] = y1I[r] + WInner - 1;

			int wc = (int)round(N*sqrt((rings - r) / (double)rings));
			if (wc % 2 == 1)
				wc++;
			x1_t[r] = (int)(jc - 0.5 - wc / 2);
			y1_t[r] = (int)(kc - 0.5 - wc / 2);

			x2_t[r] = x1_t[r] + wc - 1;
			y2_t[r] = y1_t[r] + wc - 1;
		}

		for (int f = 0; f < fLength; f++)
		{
			int id = featuresID[f];
			const int &p = descriptions[id].p;
			const int &q = descriptions[id].q;
			const int &r = descriptions[id].r;
			const int &rt = descriptions[id].rt;

			if (features[id] == 0)
			{

				double nwc = sqrt(2) / ((double)N);
				int x1Inner = 0, x2Inner = 0, y1Inner = 0, y2Inner = 0;
				if (r < rings - 1)
				{
					x1Inner = x1I[r];
					y1Inner = y1I[r];
					x2Inner = x2I[r];
					y2Inner = y2I[r];
				}

				int sOdd = sOdd_t[q];
				int q_n = q_n_t[q];
				int sMin = sMin_t[q];

				int sMax = (int)floor((p - sOdd) / 2.0);

				complex<double> M = 0;
				for (int s = sMin; s <= sMax; s++)
				{
					int ssodd2 = 2 * s + sOdd;
					double tmp_s = Alpha[p][ssodd2] * powReal(nwc, ssodd2);
					complex<double> sum_t;
					int sqn = s - q_n;
					for (int t = 0; t <= sqn; t++)
					{
						complex<double> tmp_t = (double)bc[sqn][t];
						complex<double> sum_u;
						int sqnsodd = s + q_n + sOdd;
						for (int u = 0; u <= sqnsodd; u++)
						{
							complex<double> delta(0, 0);
							if (deltasSpeeder[rt][r][t][u] == delta)
							{
								delta = deltaII(Integrals[t][u], x1_t[r], y1_t[r], (x2_t[r] - x1_t[r] + 1));
								if (r < rings - 1 && rt == 0)
									delta -= deltaII(Integrals[t][u], x1Inner, y1Inner, abs(x2Inner - x1Inner + 1));
								deltasSpeeder[rt][r][t][u] = delta;
							}
							complex<double> tmp_u = (double)bc[sqnsodd][u] * Speeder[jc2][kc2][sqn - t][sqnsodd - u]; // go back here
							sum_u += tmp_u * deltasSpeeder[rt][r][t][u];
						}
						sum_t += tmp_t * sum_u;
					}
					M += tmp_s * sum_t;
				}
				M *= M_p[p];

				features[id] = abs(M);
			}
		}
		return featuresCount;
	}

	/// <summary>Ekstrachuje cechy z podanego obrazu</summary>
	/// <param name = 'Wx'>Szerokosc okna</param>
	/// <param name = 'Wy'>Wysokosc okna</param>
	/// <param name = 'xp'>X-pozycja okna (lewy gorny rog)</param>
	/// <param name = 'yp'>Y-pozycja okna (lewy gorny rog)</param>
	/// <returns>Ekstrachowane cechy</returns>
	virtual tuple<int, const double*> extractFromWindow(int Wx, int Wy, int kp = 0, int jp = 0) override
	{
		double* features = new double[featuresCount];
		int id = -1;

		double npow2Local = (2.0 / pow(Wx, 2));
		double* M_p_Local = new double[p_max + 1];	
		for (int p = 0; p <= p_max; p++)
			M_p_Local[p] = npow2Local / (pi2 * a[p]);

		vector<double> F;
		double jc = jp + 1 + (Wx - 1) / 2.0;
		double kc = kp + 1 + (Wx - 1) / 2.0;
		int N = Wx;

		int jc2 = (int)round(jc - 0.5);
		int kc2 = (int)round(kc - 0.5);

		vector<vector<vector<vector<complex<double>>>>> deltasSpeeder(rings,
			vector<vector<vector<complex<double>>>>(ringsType + 1,
				vector<vector<complex<double>>>((p_max + q_max + 2) / 2,
					vector<complex<double>>((p_max + q_max + 2) / 2))));

		for (int r = 0; r < rings; r++)
		{
			int wc = (int)round(N*sqrt((rings - r) / (double)rings));
			if (wc % 2 == 1)
				wc++;
			int x1 = (int)(jc - 0.5 - wc / 2);
			int y1 = (int)(kc - 0.5 - wc / 2);

			int x2 = x1 + wc - 1;
			int y2 = y1 + wc - 1;

			double nwc = sqrt(2) / ((double)N);
			int x1Inner = 0, x2Inner = 0, y1Inner = 0, y2Inner = 0, WInner = 0;
			if (r < rings - 1)
			{
				WInner = (int)round(N * sqrt((rings - (r + 1)) / (double)rings));
				if (WInner % 2 == 1)
					WInner++;
				x1Inner = (int)(jc - 0.5 - WInner / 2);
				y1Inner = (int)(kc - 0.5 - WInner / 2);
				x2Inner = x1Inner + WInner - 1;
				y2Inner = y1Inner + WInner - 1;
			}

			int hMax = 0;
			if (ringsType == 1)
				hMax = (r == rings - 1) ? 0 : 1;

			for (int rt = 0; rt <= hMax; rt++)
			{
				for (int p = 0; p <= p_max; p++)
				{
					complex<double> M = M_p_Local[p];
					for (int q = 0; q <= min(p, q_max); q++)
					{
						int sOdd = sOdd_t[q];
						int q_n = q_n_t[q];
						int sMin = sMin_t[q];

						int sMax = (int)floor((p - sOdd) / 2.0);

						complex<double> sum_s(0.0);
						for (int s = sMin; s <= sMax; s++)
						{
							int ssodd2 = 2 * s + sOdd;
							double tmp_s = Alpha[p][ssodd2] * powReal(nwc, ssodd2);
							complex<double> sum_t(0.0);
							int sqn = s - q_n;
							for (int t = 0; t <= sqn; t++)
							{
								complex<double> tmp_t = (double)bc[sqn][t];
								complex<double> sum_u(0.0);
								int sqnsodd = s + q_n + sOdd;
								for (int u = 0; u <= sqnsodd; u++)
								{
									complex<double> delta(0, 0);
									if (deltasSpeeder[r][rt][t][u] == delta)
									{
										delta = deltaII(Integrals[t][u], x1, y1, (x2 - x1 + 1));
										if (r < rings - 1 && rt == 0)
											delta -= deltaII(Integrals[t][u], x1Inner, y1Inner, abs(x2Inner - x1Inner + 1));
										deltasSpeeder[r][rt][t][u] = delta;
									}
									complex<double> tmp_u = (double)bc[sqnsodd][u] * Speeder[jc2][kc2][sqn - t][sqnsodd - u]; // go back here
									sum_u += tmp_u * deltasSpeeder[r][rt][t][u];
								}
								sum_t += tmp_t * sum_u;
							}
							sum_s += tmp_s * sum_t;
						}
						features[++id] = abs(M * sum_s);
					}
				}
			}
		}
		delete[] M_p_Local;

		return make_tuple(featuresCount, features);
	}
	/// <summary>Ekstrachuje cechy z podanych plikow</summary>
	/// <param name = 'paths'>Wektor sciezek do plikow</param>
	virtual tuple<int, int, const double* const*> extractMultipleFeatures(const vector<string> &paths)  override
	{
		const double ** X = new const double*[paths.size()];

#pragma omp parallel for num_threads(OMP_NUM_THR)
		for (int i = 0; i < (int)paths.size(); i++)
		{
			PFMM pfmm(this);

			auto[fc, fets] = pfmm.extractFeatures(paths[i]);
			X[i] = fets;
		}
		return make_tuple((int)paths.size(), featuresCount, X);
	}
};
vector<int> PFMM::sMin_t = vector<int>();
vector<int> PFMM::sOdd_t = vector<int>();
vector<int> PFMM::q_n_t = vector<int>();
vector<vector<vector<vector<complex<double>>>>> PFMM::Speeder = vector<vector<vector<vector<complex<double>>>>>();
vector<vector<unsigned long long>> PFMM::bc = vector<vector<unsigned long long int>>();
vector<vector<long long>> PFMM::Alpha = vector<vector<long long>>();
vector<double> PFMM::a = vector<double>();
bool PFMM::initialized = false;


class Zernike : public Extractor
{
private:
	struct FeatureDescriptor
	{
		int r;
		int rt;
		int p;
		int q;
	};

	vector<FeatureDescriptor> descriptions;

	const int p_max;
	const int q_max;
	const int rings;
	const int ringsType;

	const double pi2 = 2 * M_PI;

	//map<int, complex<double>*> M_p_w;
	vector<vector<vector<long long>>> Beta;
	vector<double> a;

	void initializeBetas()
	{
		string fileName = "betas.bin";

		ifstream file(fileName, std::ios_base::binary);
		if (file.good())
		{
			int pSize, qSize;
			file.read(reinterpret_cast<char*>(&pSize), sizeof(pSize));
			file.read(reinterpret_cast<char*>(&qSize), sizeof(qSize));
			if (pSize != pLimit || qSize != qLimit)
			{
				file.close();
				remove(fileName.c_str());
			}
		}

		if (file.is_open() && file.good())
		{
			for (int n = 0; n <= pLimit; n++)
			{
				Beta[n].resize(n + 1);
				for (int m = n % 2; m <= n; m += 2)
				{
					Beta[n][m].resize(n + 1);
					file.read(reinterpret_cast<char*>(Beta[n][m].data()), sizeof(long long) * (n + 1));
				}
			}
			file.read(reinterpret_cast<char*>(a.data()), sizeof(double) * (pLimit + 1));
		}
		else
		{
			for (int n = 0; n <= pLimit; n++)
			{
				Beta[n].resize(n + 1);
				for (int m = n % 2; m <= n; m += 2)
				{
					Beta[n][m].resize(n + 1);
					for (int s = 0; s <= (n - m) / 2; s++)
					{
						int k = n - 2 * s;
						Beta[n][m][k] = simplificationQ(n, m, s);
					}
				}
				a[n] = (1.0 / (2 * (n + 1)));
			}

			ofstream file(fileName, std::ios_base::binary);

			file.write(reinterpret_cast<const char*>(&pLimit), sizeof(pLimit));
			file.write(reinterpret_cast<const char*>(&qLimit), sizeof(qLimit));
			for (int n = 0; n <= pLimit; n++)
				for (int m = n % 2; m <= n; m += 2)
					file.write(reinterpret_cast<const char*>(Beta[n][m].data()), sizeof(long long) * (n + 1));
			file.write(reinterpret_cast<const char*>(a.data()), sizeof(double) * (pLimit + 1));
			file.close();
		}
		if (file.is_open())
			file.close();
	}

	int simplificationQ(int n, int m, int k)
	{
		if (k == 0 && n == 0 && m == 0)
			return 1;

		vector<int> licznik(n - k - 1);
		if (n - k == 0 || n - k == 1)
		{
			licznik.resize(1);
			licznik[0] = 1;
		}
		else
			iota(licznik.begin(), licznik.end(), 2);

		vector<int> mianownik;
		mianownik.push_back(1);
		for (int i = 0; i < k - 1; i++)
			mianownik.push_back(i + 2);
		for (int i = 0; i < (n + m) / 2.0 - k - 1; i++)
			mianownik.push_back(i + 2);
		for (int i = 0; i < (n - m) / 2.0 - k - 1; i++)
			mianownik.push_back(i + 2);

		vector<int> ind_licznik(licznik.size());
		fill(ind_licznik.begin(), ind_licznik.end(), 1);

		vector<int> ind_mianownik(mianownik.size());
		fill(ind_mianownik.begin(), ind_mianownik.end(), 1);

		while (true)
		{
			for (int i = 0; i < (int)mianownik.size(); i++)
			{
				for (int j = 0; j < (int)licznik.size(); j++)
				{
					int a = gcd(licznik[j], mianownik[i]);
					if (a > 1)
					{
						licznik[j] /= a;
						mianownik[i] /= a;
						break;
					}
				}
			}
			vector<int> res_licznik;
			vector<int> res_mianownik;
			for (int i = 0; i < (int)mianownik.size(); i++)
				if (mianownik[i] != 1)
					res_mianownik.push_back(mianownik[i]);
			for (int i = 0; i < (int)licznik.size(); i++)
				if (licznik[i] != 1)
					res_licznik.push_back(licznik[i]);
			if (licznik.size() == res_licznik.size() && mianownik.size() == res_mianownik.size())
				break;
			mianownik = res_mianownik;
			licznik = res_licznik;
		}

		licznik.push_back(1);
		mianownik.push_back(1);
		return (int)(pow(-1, k) * prod(licznik) / prod(mianownik));
	}

public:
	using Extractor::extractFromWindow;

	~Zernike()
	{
		//for (map<int, complex<double>*>::iterator iter = M_p_w.begin(); iter != M_p_w.end(); ++iter)
		//	delete[] M_p_w[iter->first];
		//M_p_w.clear();
	}

	Zernike(const int p_max, const int q_max, const int rings, const int ringsType, SaveFileType fileType = SaveFileType::text) : p_max{ p_max }, q_max{ q_max }, rings{ rings }, ringsType{ ringsType }
	{
		this->saveMode = fileType;

		Beta = vector<vector<vector<long long>>>();
		a = vector<double>();
		Beta.resize(Extractor::pLimit + 1);
		a.resize(Extractor::pLimit + 1);

		initializeBetas();

		for (int r = 0; r < rings; r++)
		{
			int hMax = 0;
			if (ringsType == 1)
				hMax = (r == rings - 1) ? 0 : 1;

			for (int rt = 0; rt <= hMax; rt++)
			{
				for (int p = 0; p <= p_max; p++)
				{
					for (int q = p % 2; q <= min(p, q_max); q += 2)
					{
						descriptions.push_back(FeatureDescriptor{ r, rt, p, q });
					}
				}
			}
		}
	}

	static string GetType()
	{
		return "ZernikeExtractorNoII";
	}

	string getType() const override
	{
		return GetType();
	}

	bool getRectangleWindowsRequirement() const override
	{
		return true;
	}

	string toString() const override
	{
		string text = getType() + "\r\n";
		text += "Pmax: " + to_string(p_max) + "\r\n";
		text += "Qmax: " + to_string(q_max) + "\r\n";
		text += "Rings: " + to_string(rings) + "\r\n";
		text += "RingsType: " + to_string(ringsType) + "\r\n";

		return text;
	}

	const double* const* img = nullptr;
	void loadImageData(const string path) override
	{
		throw exception("Not implemented");
	}

	virtual void loadImageData(const double* const* img, int height, int width) override
	{
		this->width = width - width % 2;
		this->height = height - height % 2;

		this->img = img;
	}

	virtual void clearImageData() override
	{
		throw exception("Not implemented");
	}

	void initializeExtractor(Point* sizes, int sca) override
	{
		throw exception("Not implemented");
	}

	/// <param name = 'featuresID'>Numery cech</param>
	/// <param name = 'Wx'>Szerokosc okna</param>
	/// <param name = 'Wy'>Wysokosc okna</param>
	/// <param name = 'xp'>X-pozycja okna (lewy gorny rog)</param>
	/// <param name = 'yp'>Y-pozycja okna (lewy gorny rog)</param>
	virtual int extractFromWindow(double* features, const int* featuresID, int fLength, int Wx, int Wy, int kp = 0, int jp = 0) override
	{
		throw exception("Not implemented");
	}


	/// <summary>Ekstrachuje cechy z podanego obrazu</summary>
	/// <param name = 'Wx'>Szerokosc okna</param>
	/// <param name = 'Wy'>Wysokosc okna</param>
	/// <param name = 'xp'>X-pozycja okna (lewy gorny rog)</param>
	/// <param name = 'yp'>Y-pozycja okna (lewy gorny rog)</param>
	/// <returns>Ekstrachowane cechy</returns>
	virtual tuple<int, const double*> extractFromWindow(int Wx, int Wy, int kp = 0, int jp = 0) override
	{
		double* features = new double[descriptions.size()];
		int id = -1;

		double npow2Local = (2.0 / pow(Wx, 2));
		double* M_p_Local = new double[p_max + 1];
		for (int p = 0; p <= p_max; p++)
			M_p_Local[p] = npow2Local / (pi2 * a[p]);

		double jc = jp + 1 + (Wx - 1) / 2.0;
		double kc = kp + 1 + (Wx - 1) / 2.0;
		int N = Wx;

		int jc2 = (int)round(jc - 0.5);
		int kc2 = (int)round(kc - 0.5);

		for (int r = 0; r < rings; r++)
		{
			int wc = (int)round(N * sqrt((rings - r) / (double)rings));
			if (wc % 2 == 1)
				wc++;
			int x1 = (int)(kc - 0.5 - wc / 2);
			int y1 = (int)(jc - 0.5 - wc / 2);

			int x2 = x1 + wc - 1;
			int y2 = y1 + wc - 1;

			double nwc = sqrt(2) / ((double)N);
			int x1Inner = 0, x2Inner = 0, y1Inner = 0, y2Inner = 0, WInner = 0;
			if (r < rings - 1)
			{
				WInner = (int)round(N * sqrt((rings - (r + 1)) / (double)rings));
				if (WInner % 2 == 1)
					WInner++;
				x1Inner = (int)(kc - 0.5 - WInner / 2);
				y1Inner = (int)(jc - 0.5 - WInner / 2);
				x2Inner = x1Inner + WInner - 1;
				y2Inner = y1Inner + WInner - 1;
			}

			int hMax = 0;
			if (ringsType == 1)
				hMax = (r == rings - 1) ? 0 : 1;

			for (int rt = 0; rt <= hMax; rt++)
			{
				for (int p = 0; p <= p_max; p++)
				{
					complex<double> M = M_p_Local[p];
					for (int q = p % 2; q <= min(p, q_max); q += 2)
					{
						complex<double> suma(0.0);
						for (int y = y1; y <= y2; y++)
						{
							for (int x = x1; x <= x2; x++)
							{
								double xk = (2.0 * (x - kp) - (Wx - 1)) / (Wx * sqrt(2));
								double yj = ((Wx - 1) - 2.0 * (y - jp)) / (Wx * sqrt(2));
								complex<double> sum_s(0.0);
								for (int s = q; s <= p; s += 2)
								{
									double beta = Beta[p][q][s];// *powReal(sqrt(2) / (double)Wx, s);
									//system("PAUSE");
									sum_s += beta * (pow(complex<double>(xk, -yj), 1.0 / 2 * (s - q)))
										* pow(complex<double>(xk, +yj), 1.0 / 2 * (s + q));
								}
								suma += sum_s * img[y][x];
							}
						}
						if (r < rings - 1 && rt == 0)
						{
							for (int y = y1Inner; y <= y2Inner; y++)
							{
								for (int x = x1Inner; x <= x2Inner; x++)
								{
									double xk = (2.0 * (x - kp) - (Wx - 1)) / (Wx * sqrt(2));
									double yj = ((Wx - 1) - 2.0 * (y - jp)) / (Wx * sqrt(2));
									complex<double> sum_s(0.0);
									for (int s = q; s <= p; s += 2)
									{
										double beta = Beta[p][q][s];// *powReal(sqrt(2) / (double)Wx, s);
										//system("PAUSE");
										sum_s += beta * (pow(complex<double>(xk, -yj), 1.0 / 2 * (s - q)))
											* pow(complex<double>(xk, +yj), 1.0 / 2 * (s + q));
									}
									suma -= sum_s * img[y][x];
								}
							}
						}
						//cout << abs(M*suma) << endl;
						//system("PAUSE");
						features[++id] = abs(M * suma);
					}
				}
			}
		}

		delete[] M_p_Local;

		return make_tuple((int)descriptions.size(), features);
	}

	virtual tuple<int, int, const double* const*> extractMultipleFeatures(const vector<string>& paths)  override
	{
		throw exception("Not implemented");
	}
};

class ZernikeII : public Extractor
{
protected:
	//vector<vector<vector<vector<complex<double>>>>> Integrals;
	complex<double> ****Integrals = nullptr;

	static vector<vector<vector<vector<complex<double>>>>> Speeder;
	static vector<vector<unsigned long long int>> bc;
	static vector<vector<vector<long long>>> Beta;
	static vector<double> a;

	static bool initialized;

	static void initializeBetas()
	{
		string fileName = "betas.bin";

		ifstream file(fileName, std::ios_base::binary);
		if (file.good())
		{
			int pSize, qSize;
			file.read(reinterpret_cast<char*>(&pSize), sizeof(pSize));
			file.read(reinterpret_cast<char*>(&qSize), sizeof(qSize));
			if (pSize != pLimit || qSize != qLimit)
			{
				file.close();
				remove(fileName.c_str());
			}
		}

		if (file.is_open() && file.good())
		{
			for (int n = 0; n <= pLimit; n++)
			{
				Beta[n].resize(n + 1);
				for (int m = n % 2; m <= n; m += 2)
				{
					Beta[n][m].resize(n + 1);
					file.read(reinterpret_cast<char*>(Beta[n][m].data()), sizeof(long long) * (n + 1));
				}
			}
			file.read(reinterpret_cast<char*>(a.data()), sizeof(double) * (pLimit + 1));
		}
		else
		{
			for (int n = 0; n <= pLimit; n++)
			{
				Beta[n].resize(n + 1);
				for (int m = n % 2; m <= n; m += 2)
				{
					Beta[n][m].resize(n + 1);
					for (int s = 0; s <= (n - m) / 2; s++)
					{
						int k = n - 2 * s;
						Beta[n][m][k] = simplificationQ(n, m, s);
					}
				}
				a[n] = (1.0 / (2 * (n + 1)));
			}

			ofstream file(fileName, std::ios_base::binary);

			file.write(reinterpret_cast<const char*>(&pLimit), sizeof(pLimit));
			file.write(reinterpret_cast<const char*>(&qLimit), sizeof(qLimit));
			for (int n = 0; n <= pLimit; n++)
				for (int m = n % 2; m <= n; m += 2)
					file.write(reinterpret_cast<const char*>(Beta[n][m].data()), sizeof(long long) * (n + 1));
			file.write(reinterpret_cast<const char*>(a.data()), sizeof(double) * (pLimit + 1));
			file.close();
		}
		if (file.is_open())
			file.close();
	}

	static int simplificationQ(int n, int m, int k)
	{
		if (k == 0 && n == 0 && m == 0)
			return 1;

		vector<int> licznik(n - k - 1);
		if (n - k == 0 || n - k == 1)
		{
			licznik.resize(1);
			licznik[0] = 1;
		}
		else
			iota(licznik.begin(), licznik.end(), 2);

		vector<int> mianownik;
		mianownik.push_back(1);
		for (int i = 0; i < k - 1; i++)
			mianownik.push_back(i + 2);
		for (int i = 0; i < (n + m) / 2.0 - k - 1; i++)
			mianownik.push_back(i + 2);
		for (int i = 0; i < (n - m) / 2.0 - k - 1; i++)
			mianownik.push_back(i + 2);

		vector<int> ind_licznik(licznik.size());
		fill(ind_licznik.begin(), ind_licznik.end(), 1);

		vector<int> ind_mianownik(mianownik.size());
		fill(ind_mianownik.begin(), ind_mianownik.end(), 1);

		while (true)
		{
			for (int i = 0; i < (int)mianownik.size(); i++)
			{
				for (int j = 0; j < (int)licznik.size(); j++)
				{
					int a = gcd(licznik[j], mianownik[i]);
					if (a > 1)
					{
						licznik[j] /= a;
						mianownik[i] /= a;
						break;
					}
				}
			}
			vector<int> res_licznik;
			vector<int> res_mianownik;
			for (int i = 0; i < (int)mianownik.size(); i++)
				if (mianownik[i] != 1)
					res_mianownik.push_back(mianownik[i]);
			for (int i = 0; i < (int)licznik.size(); i++)
				if (licznik[i] != 1)
					res_licznik.push_back(licznik[i]);
			if (licznik.size() == res_licznik.size() && mianownik.size() == res_mianownik.size())
				break;
			mianownik = res_mianownik;
			licznik = res_licznik;
		}

		licznik.push_back(1);
		mianownik.push_back(1);
		return (int)(pow(-1, k)*prod(licznik) / prod(mianownik));
	}

	static void initialzeBinomials()
	{
		bc.clear();

		string fileName = "binomials.bin";
		int n = pLimit + 2 * qLimit + 1;

		ifstream file(fileName, std::ios_base::binary);
		if (file.good())
		{
			int qSize, pSize;
			file.read(reinterpret_cast<char*>(&qSize), sizeof(qSize));
			file.read(reinterpret_cast<char*>(&pSize), sizeof(pSize));
			if (pSize != pLimit || qSize != qLimit)
			{
				file.close();
				remove(fileName.c_str());
			}
		}

		if (file.is_open() && file.good())
		{
			bc.resize(n);
			for (int i = 0; i < n; i++)
			{
				bc[i].resize(i + 1);
				file.read(reinterpret_cast<char*>(bc[i].data()), sizeof(unsigned long long) * (i + 1));
			}
		}
		else
		{
			binomial(pLimit, qLimit, bc);

			ofstream file(fileName, std::ios_base::binary);
			file.write(reinterpret_cast<const char*>(&qLimit), sizeof(qLimit));
			file.write(reinterpret_cast<const char*>(&pLimit), sizeof(pLimit));
			for (int i = 0; i < n; i++)
				file.write(reinterpret_cast<const char*>(bc[i].data()), sizeof(unsigned long long) * (i + 1));
			file.close();
		}
		if (file.is_open())
			file.close();
	}

	static void initialzeSpeeder()
	{
		Speeder.clear();

		string fileName = "speeders.bin";

		int maxExponent = (int)ceil(pLimit / 2.0 + qLimit / 2.0);

		ifstream file(fileName, std::ios_base::binary);
		if (file.good())
		{
			int wSize, qSize, pSize;
			file.read(reinterpret_cast<char*>(&wSize), sizeof(wSize));
			file.read(reinterpret_cast<char*>(&qSize), sizeof(qSize));
			file.read(reinterpret_cast<char*>(&pSize), sizeof(pSize));
			if (pSize != pLimit || qSize != qLimit || wSize != predictedMaxWindowSize)
			{
				file.close();
				remove(fileName.c_str());
			}
		}

		if (file.is_open() && file.good())
		{
			Speeder.resize(predictedMaxWindowSize);
			for (int i = 0; i < predictedMaxWindowSize; i++)
			{
				Speeder[i].resize(predictedMaxWindowSize);
				for (int j = 0; j < predictedMaxWindowSize; j++)
				{
					Speeder[i][j].resize(maxExponent + 1);
					for (int expT = 0; expT <= maxExponent; expT++)
					{
						Speeder[i][j][expT].resize(maxExponent + 2);
						file.read(reinterpret_cast<char*>(Speeder[i][j][expT].data()), sizeof(complex<double>) * (maxExponent + 2));
					}
				}
			}
		}
		else
		{
			speeder(predictedMaxWindowSize);

			ofstream file(fileName, std::ios_base::binary);
			file.write(reinterpret_cast<const char*>(&predictedMaxWindowSize), sizeof(predictedMaxWindowSize));
			file.write(reinterpret_cast<const char*>(&qLimit), sizeof(qLimit));
			file.write(reinterpret_cast<const char*>(&pLimit), sizeof(pLimit));
			for (int i = 0; i < predictedMaxWindowSize; i++)
				for (int j = 0; j < predictedMaxWindowSize; j++)
					for (int expT = 0; expT <= maxExponent; expT++)
						file.write(reinterpret_cast<const char*>(Speeder[i][j][expT].data()), sizeof(complex<double>) * (maxExponent + 2));
			file.close();
		}
		if (file.is_open())
			file.close();
	}

	static void speeder(int maxsize)
	{
		Speeder.clear();

		int s1 = maxsize;
		Speeder.resize(s1);
		int maxExponent = (int)ceil(pLimit / 2.0 + qLimit / 2.0);
		for (int j = 0; j < s1; j++)
		{
			double jc = j + 0.5;
			Speeder[j] = vector<vector<vector<complex<double>>>>(s1);
			for (int k = 0; k < s1; k++)
			{
				double kc = k + 0.5;
				Speeder[j][k] = vector<vector<complex<double>>>(maxExponent + 1);
				for (int expT = 0; expT <= maxExponent; expT++)
				{
					Speeder[j][k][expT] = vector<complex<double>>(maxExponent + 2);
					complex<double> factorT = pow(complex<double>(-kc, jc), expT);
					for (int expU = 0; expU <= maxExponent + 1; expU++)
					{
						complex<double> factorU = pow(complex<double>(-kc, -jc), expU);
						Speeder[j][k][expT][expU] = factorT * factorU;
					}
				}
			}
		}
	}

	static void speederUpdate(int maxsize)
	{
		int minsize = (int)Speeder.size();

		int s1 = maxsize;
		int maxExponent = (int)ceil(pLimit / 2.0 + qLimit / 2.0);

		// doliczenie nowych kolumn dla istniejacych wierszy
		for (int j = 0; j < minsize; j++)
		{
			double jc = j + 0.5;
			Speeder[j].resize(s1);
			for (int k = minsize; k < s1; k++)
			{
				double kc = k + 0.5;
				Speeder[j][k] = vector<vector<complex<double>>>(maxExponent + 1);
				for (int expT = 0; expT <= maxExponent; expT++)
				{
					Speeder[j][k][expT] = vector<complex<double>>(maxExponent + 2);
					complex<double> factorT = pow(complex<double>(-kc, jc), expT);
					for (int expU = 0; expU <= maxExponent + 1; expU++)
					{
						complex<double> factorU = pow(complex<double>(-kc, -jc), expU);
						Speeder[j][k][expT][expU] = factorT * factorU;
					}
				}
			}
		}

		// doliczenie nowych wierszy
		Speeder.resize(s1);
		for (int j = minsize; j < s1; j++)
		{
			double jc = j + 0.5;
			Speeder[j] = vector<vector<vector<complex<double>>>>(s1);
			for (int k = 0; k < s1; k++)
			{
				double kc = k + 0.5;
				Speeder[j][k] = vector<vector<complex<double>>>(maxExponent + 1);
				for (int expT = 0; expT <= maxExponent; expT++)
				{
					Speeder[j][k][expT] = vector<complex<double>>(maxExponent + 2);
					complex<double> factorT = pow(complex<double>(-kc, jc), expT);
					for (int expU = 0; expU <= maxExponent + 1; expU++)
					{
						complex<double> factorU = pow(complex<double>(-kc, -jc), expU);
						Speeder[j][k][expT][expU] = factorT * factorU;
					}
				}
			}
		}
	}

	void calculateII2(const double* const* img)
	{
		//ofstream myfile;
		//myfile.open("timelogs_integrals.txt");

		//std::chrono::system_clock::time_point beginii = std::chrono::system_clock::now();
		//int inregrals = 0;

		clearImageData();

		int s1 = width;
		int s2 = height;
	
		int tlim = p_max / 2;
		int tg = (int)ceil((p_max - min(q_max, p_max)) / 2.0);
		int maxu = p_max - tg;
		Integrals = new complex<double>***[tlim + 1];
		for (int t = 0; t <= tlim; t++)
		{
			if (t > tg)
				maxu = p_max - t;
			Integrals[t] = new complex<double>**[maxu + 1];
			#pragma omp parallel for num_threads(OMP_NUM_THR)
			for (int u = t; u <= maxu; u++)
			{
				complex<double>* ll = new complex<double>[s1];
				//inregrals++;
				Integrals[t][u] = new complex<double>*[s2];
				for (int y = 0; y < s2; y++)
				{
					Integrals[t][u][y] = new complex<double>[s1];
					for (int x = 0; x < s1; x++)
					{
						complex<double> a = img[y][x] * (powIntQuick(powInt(x + 1, 2) + powInt(y + 1, 2), t)
							* powComQuick(complex<double>(x + 1, y + 1), u - t));
						complex<double> s;
						if (x > 0)
							s = ll[x - 1] + a;
						else
							s = a;
						ll[x] = s;
						if (y > 0)
							s = s + Integrals[t][u][y - 1][x];
						Integrals[t][u][y][x] = s;
					}
				}
				delete[] ll;
			}
		}

		maxu = p_max - tg;
		for (int t = 0; t <= tlim; t++)
		{
			if (t > tg)
				maxu = p_max - t;

			#pragma omp parallel for num_threads(OMP_NUM_THR)
			for (int u = 0; u < t; u++)
			{
				Integrals[t][u] = new complex<double>*[s2];
				for (int y = 0; y < s2; y++)
				{
					Integrals[t][u][y] = new complex<double>[s1];
					for (int x = 0; x < s1; x++)
					{
						Integrals[t][u][y][x] = conj(Integrals[u][t][y][x]);
					}
				}
			}
		}

		//std::chrono::system_clock::time_point endii = std::chrono::system_clock::now();
		////myfile << "All Integrals" << inregrals << endl;
		//myfile << "All Integral time" << endl;
		//myfile << "Time difference = " << std::chrono::duration_cast<std::chrono::nanoseconds>(endii - beginii).count() << "ns" << endl;
		//myfile << "Time difference = " << std::chrono::duration_cast<std::chrono::milliseconds> (endii - beginii).count() << "ms" << endl;
		//myfile.close();
	}

	complex<double> deltaII(const complex<double>* const* Integral, const int jp, const int kp, const int N) const
	{
		complex<double> delta;
		if (jp == 0 && kp == 0)
			delta = Integral[jp + N - 1][kp + N - 1];
		else if (jp == 0)
			delta = Integral[jp + N - 1][kp + N - 1] - Integral[jp + N - 1][kp - 1];
		else if (kp == 0)
			delta = Integral[jp + N - 1][kp + N - 1] - Integral[jp - 1][kp + N - 1];
		else
			delta = Integral[jp + N - 1][kp + N - 1] - Integral[jp + N - 1][kp - 1]
			- Integral[jp - 1][kp + N - 1] + Integral[jp - 1][kp - 1];
		return delta;
	}

	struct FeatureDescriptor
	{
		int r;
		int rt;
		int p;
		int q;
	};

	vector<FeatureDescriptor> descriptions;

	const int p_max;
	const int q_max;
	const int rings;
	const int ringsType;

	static constexpr double pi2 = 2 * M_PI;
	map<int, complex<double>*> M_p_w;

	ZernikeII(ZernikeII * parent) : p_max{ parent->p_max }, q_max{ parent->q_max }, rings{ parent->rings }, ringsType{ parent->ringsType }
	{
		this->saveMode = parent->saveMode;

		for (map<int, complex<double>*>::iterator iter = M_p_w.begin(); iter != M_p_w.end(); ++iter)
		{
			int k = iter->first;

			this->M_p_w[k] = new complex<double>[p_max + 1];
			for (int p = 0; p <= p_max; p++)
				this->M_p_w[k][p] = parent->M_p_w[k][p];
		}

		this->featuresCount = parent->featuresCount;
		this->descriptions = parent->descriptions;
	}
public:
	using Extractor::extractFromWindow;

	static void initializeExtractor()
	{
		if (!initialized)
		{
			Beta.resize(ZernikeII::pLimit + 1);
			a.resize(ZernikeII::pLimit + 1);

			initializeBetas();
			initialzeBinomials();
			initialzeSpeeder();

			initialized = true;
		}
	}

	static void clearMemory()
	{
		Speeder = vector<vector<vector<vector<complex<double>>>>>();
		bc = vector<vector<unsigned long long int>>();
		Beta = vector<vector<vector<long long>>>();
		a = vector<double>();

		initialized = false;
	}

	~ZernikeII()
	{
		clearImageData();

		for (map<int, complex<double>*>::iterator iter = M_p_w.begin(); iter != M_p_w.end(); ++iter)
			delete[] M_p_w[iter->first];
		M_p_w.clear();

	}

	ZernikeII() : ZernikeII(8, 8, 6, 0, SaveFileType::text) {}

	ZernikeII(const int p_max, const int q_max, const int rings, const int ringsType, SaveFileType fileType = SaveFileType::text) : p_max{ p_max }, q_max{ q_max }, rings{ rings }, ringsType{ ringsType }
	{
		if (!initialized)
			initializeExtractor();

		this->saveMode = fileType;

		for (int r = 0; r < rings; r++)
		{
			int hMax = 0;
			if (ringsType == 1)
				hMax = (r == rings - 1) ? 0 : 1;

			for (int rt = 0; rt <= hMax; rt++)
			{
				for (int p = 0; p <= p_max; p++)
				{
					for (int q = p % 2; q <= min(p, q_max); q += 2)
						//for (int q = p % 2; q <= p; q += 2)
					{
						descriptions.push_back(FeatureDescriptor{ r, rt, p, q });
					}
				}
			}
		}
		featuresCount = (int)descriptions.size();
	}

	static string GetType()
	{
		return "ZernikeExtractor";
	}

	/// <summary>Zwraca typ cechy</summary>
	/// <returns>Typ klasyfikatora</returns>
	virtual string getType() const override
	{
		return GetType();
	}

	bool getRectangleWindowsRequirement() const override
	{
		return true;
	}

	/// <summary>Zwraca opis ekstraktora cech</summary>
	/// <returns>Opis ekstraktora cech</returns>
	virtual string toString() const override
	{
		string text = getType() + "\r\n";
		text += "Pmax: " + to_string(p_max) + "\r\n";
		text += "Qmax: " + to_string(q_max) + "\r\n";
		text += "Rings: " + to_string(rings) + "\r\n";
		text += "RingsType: " + to_string(ringsType) + "\r\n";

		return text;
	}

	virtual void loadImageData(const string path) override
	{
		clearImageData();

		auto[height, width, img] = loadImage(path, saveMode);
		this->width = width;
		this->height = height;

		int maxDim = max(width, height);

		calculateII2(img);

#pragma omp critical (RecalculateSpeeder)
		{
			if ((int)Speeder.size() < maxDim)
				speederUpdate(width);
		}

		for (int i = 0; i < height; i++)
			delete[] img[i];
		delete[] img;
	}

	virtual void loadImageData(const double* const* img, int height, int width) override
	{
		clearImageData();

		this->width = width - width%2;
		this->height = height - height%2;
		int maxDim = max(width, height);

		calculateII2(img);

#pragma omp critical (RecalculateSpeeder)
		{
			if ((int)Speeder.size() < maxDim)
				speederUpdate(maxDim);
		}
	}

	virtual void clearImageData() override
	{
		if (Integrals != nullptr)
		{
			int s1 = width;
			int s2 = height;
			int tlim = p_max / 2;
			int tg = (int)ceil((p_max - min(q_max, p_max)) / 2.0);
			int maxu = p_max - tg;
			for (int t = 0; t <= tlim; t++)
			{
				if (t > tg)
					maxu = p_max - t;
				for (int u = 0; u <= maxu; u++)
				{
					for (int y = 0; y < s2; y++)
					{
						delete[] Integrals[t][u][y];
					}
					delete[] Integrals[t][u];
				}
				delete[] Integrals[t];
			}
			delete[] Integrals;
		}
		Integrals = nullptr;
	}

	void initializeExtractor(Point* sizes, int sca) override
	{
		if (!initialized)
			initializeExtractor();

		for (int s1 = 0; s1 < sca; s1++)
		{
			int Wx = sizes[s1].wx;

			if (M_p_w.count(Wx) == 0)
			{
				this->M_p_w[Wx] = new complex<double>[p_max + 1];
				for (int p = 0; p <= p_max; p++)
					this->M_p_w[Wx][p] = (2.0 / pow(Wx, 2)) / (pi2 * a[p]);
			}
		}
	}

	int extractFromWindowSmall(double* features, const int* featuresID, int fLength, int Wx, int Wy, int kp = 0, int jp = 0)
	{
		complex<double>* M_p = M_p_w[Wx];

		double jc = jp + 1 + (Wx - 1) / 2.0;
		double kc = kp + 1 + (Wx - 1) / 2.0;
		int N = Wx;

		int jc2 = (int)round(jc - 0.5);
		int kc2 = (int)round(kc - 0.5);

		for (int f = 0; f < fLength; f++)
		{
			int id = featuresID[f];
			const int &p = descriptions[id].p;
			const int &q = descriptions[id].q;
			const int &r = descriptions[id].r;
			const int &rt = descriptions[id].rt;

			double nwc = sqrt(2) / ((double)N);

			int WInner = (int)round(N * sqrt((rings - (r + 1)) / (double)rings));
			if (WInner % 2 == 1)
				WInner++;
			int x1Inner = 0, x2Inner = 0, y1Inner = 0, y2Inner = 0;
			if (r < rings - 1)
			{
				x1Inner = (int)(jc - 0.5 - WInner / 2);
				y1Inner = (int)(kc - 0.5 - WInner / 2);
				x2Inner = x1Inner + WInner - 1;
				y2Inner = y1Inner + WInner - 1;
			}
			int wc = (int)round(N*sqrt((rings - r) / (double)rings));
			if (wc % 2 == 1)
				wc++;
			int x1_t = (int)(jc - 0.5 - wc / 2);
			int y1_t = (int)(kc - 0.5 - wc / 2);
			int x2_t = x1_t + wc - 1;
			int y2_t = y1_t + wc - 1;

			complex<double> M = 0;
			for (int s = q; s <= p; s += 2)
			{
				double tmp_s = Beta[p][q][s] * powReal(nwc, s);
				complex<double> sum_t;
				int sqn = (int)((s - q) / 2.0);
				for (int t = 0; t <= sqn; t++)
				{
					complex<double> tmp_t = (double)bc[sqn][t];
					complex<double> sum_u;
					int sqnsodd = (int)((s + q) / 2.0);
					for (int u = 0; u <= sqnsodd; u++)
					{
						complex<double> delta(0, 0);
						//if (deltasSpeeder[rt][r][t][u] == delta)
						//{
						delta = deltaII(Integrals[t][u], x1_t, y1_t, (x2_t - x1_t + 1));
						//delta = deltaII(Integrals[t][u], x1_t[r], y1_t[r], (x2_t[r] - x1_t[r] + 1));
						if (r < rings - 1 && rt == 0)
							delta -= deltaII(Integrals[t][u], x1Inner, y1Inner, abs(x2Inner - x1Inner + 1));
						//deltasSpeeder[rt][r][t][u] = delta;
					//}
						complex<double> tmp_u = (double)bc[sqnsodd][u] * Speeder[jc2][kc2][sqn - t][sqnsodd - u]; // go back here
						sum_u += tmp_u * delta; // deltasSpeeder[rt][r][t][u];
					}
					sum_t += tmp_t * sum_u;
				}
				M += tmp_s * sum_t;
			}
			M *= M_p[p];

			features[id] = abs(M);
		}
		return featuresCount;
	}

	/// <param name = 'featuresID'>Numery cech</param>
	/// <param name = 'Wx'>Szerokosc okna</param>
	/// <param name = 'Wy'>Wysokosc okna</param>
	/// <param name = 'xp'>X-pozycja okna (lewy gorny rog)</param>
	/// <param name = 'yp'>Y-pozycja okna (lewy gorny rog)</param>
	virtual int extractFromWindow(double* features, const int* featuresID, int fLength, int Wx, int Wy, int kp = 0, int jp = 0) override
	{
		if (fLength < 30)
		{
			return extractFromWindowSmall(features, featuresID, fLength, Wx, Wy, kp, jp);
		}
		else
		{
			complex<double>* M_p = M_p_w[Wx];

			//return extractFromWindow(Wx, Wy, xp, yp);
			double jc = jp + 1 + (Wx - 1) / 2.0;
			double kc = kp + 1 + (Wx - 1) / 2.0;
			int N = Wx;

			vector<vector<vector<vector<complex<double>>>>> deltasSpeeder(ringsType + 1,
				vector<vector<vector<complex<double>>>>(rings,
					vector<vector<complex<double>>>((p_max + q_max + 2) / 2,
						vector<complex<double>>((p_max + q_max + 2) / 2))));

			int jc2 = (int)round(jc - 0.5);
			int kc2 = (int)round(kc - 0.5);

			vector<int> x1I(rings), y1I(rings), x2I(rings), y2I(rings);
			vector<int> x1_t(rings), y1_t(rings), x2_t(rings), y2_t(rings);
			for (int r = 0; r < rings; r++)
			{
				int WInner = (int)round(N * sqrt((rings - (r + 1)) / (double)rings));
				if (WInner % 2 == 1)
					WInner++;
				x1I[r] = (int)(jc - 0.5 - WInner / 2);
				y1I[r] = (int)(kc - 0.5 - WInner / 2);
				x2I[r] = x1I[r] + WInner - 1;
				y2I[r] = y1I[r] + WInner - 1;

				int wc = (int)round(N*sqrt((rings - r) / (double)rings));
				if (wc % 2 == 1)
					wc++;
				x1_t[r] = (int)(jc - 0.5 - wc / 2);
				y1_t[r] = (int)(kc - 0.5 - wc / 2);

				x2_t[r] = x1_t[r] + wc - 1;
				y2_t[r] = y1_t[r] + wc - 1;
			}

			for (int f = 0; f < fLength; f++)
			{
				int id = featuresID[f];
				const int &p = descriptions[id].p;
				const int &q = descriptions[id].q;
				const int &r = descriptions[id].r;
				const int &rt = descriptions[id].rt;

				double nwc = sqrt(2) / ((double)N);
				int x1Inner = 0, x2Inner = 0, y1Inner = 0, y2Inner = 0;
				if (r < rings - 1)
				{
					x1Inner = x1I[r];
					y1Inner = y1I[r];
					x2Inner = x2I[r];
					y2Inner = y2I[r];
				}

				complex<double> M = 0;
				for (int s = q; s <= p; s += 2)
				{
					double tmp_s = Beta[p][q][s] * powReal(nwc, s);
					complex<double> sum_t;
					int sqn = (int)((s - q) / 2.0);
					for (int t = 0; t <= sqn; t++)
					{
						complex<double> tmp_t = (double)bc[sqn][t];
						complex<double> sum_u;
						int sqnsodd = (int)((s + q) / 2.0);
						for (int u = 0; u <= sqnsodd; u++)
						{
							complex<double> delta(0, 0);
							if (deltasSpeeder[rt][r][t][u] == delta)
							{
								delta = deltaII(Integrals[t][u], x1_t[r], y1_t[r], (x2_t[r] - x1_t[r] + 1));
								if (r < rings - 1 && rt == 0)
									delta -= deltaII(Integrals[t][u], x1Inner, y1Inner, abs(x2Inner - x1Inner + 1));
								deltasSpeeder[rt][r][t][u] = delta;
							}
							complex<double> tmp_u = (double)bc[sqnsodd][u] * Speeder[jc2][kc2][sqn - t][sqnsodd - u]; // go back here
							sum_u += tmp_u * deltasSpeeder[rt][r][t][u];
						}
						sum_t += tmp_t * sum_u;
					}
					M += tmp_s * sum_t;
				}
				M *= M_p[p];

				features[id] = abs(M);
			}
			return featuresCount;
		}
	}

	/// <summary>Ekstrachuje cechy z podanego obrazu</summary>
	/// <param name = 'Wx'>Szerokosc okna</param>
	/// <param name = 'Wy'>Wysokosc okna</param>
	/// <param name = 'xp'>X-pozycja okna (lewy gorny rog)</param>
	/// <param name = 'yp'>Y-pozycja okna (lewy gorny rog)</param>
	/// <returns>Ekstrachowane cechy</returns>
	virtual tuple<int, const double*> extractFromWindow(int Wx, int Wy, int kp = 0, int jp = 0) override
	{
		double* features = new double[featuresCount];
		int id = -1;

		double npow2Local = (2.0 / pow(Wx, 2));
		double* M_p_Local = new double[p_max + 1];
		for (int p = 0; p <= p_max; p++)
			M_p_Local[p] = npow2Local / (pi2 * a[p]);

		//vector<double> F;
		double jc = jp + 1 + (Wx - 1) / 2.0;
		double kc = kp + 1 + (Wx - 1) / 2.0;
		int N = Wx;

		int jc2 = (int)round(jc - 0.5);
		int kc2 = (int)round(kc - 0.5);

		vector<vector<vector<vector<complex<double>>>>> deltasSpeeder(rings,
			vector<vector<vector<complex<double>>>>(ringsType + 1,
				vector<vector<complex<double>>>((p_max + q_max + 2) / 2,
					vector<complex<double>>((p_max + q_max + 2) / 2))));

		for (int r = 0; r < rings; r++)
		{
			int wc = (int)round(N*sqrt((rings - r) / (double)rings));
			if (wc % 2 == 1)
				wc++;
			int x1 = (int)(jc - 0.5 - wc / 2);
			int y1 = (int)(kc - 0.5 - wc / 2);

			int x2 = x1 + wc - 1;
			int y2 = y1 + wc - 1;

			double nwc = sqrt(2) / ((double)N);
			int x1Inner = 0, x2Inner = 0, y1Inner = 0, y2Inner = 0, WInner = 0;
			if (r < rings - 1)
			{
				WInner = (int)round(N * sqrt((rings - (r + 1)) / (double)rings));
				if (WInner % 2 == 1)
					WInner++;
				x1Inner = (int)(jc - 0.5 - WInner / 2);
				y1Inner = (int)(kc - 0.5 - WInner / 2);
				x2Inner = x1Inner + WInner - 1;
				y2Inner = y1Inner + WInner - 1;
			}

			int hMax = 0;
			if (ringsType == 1)
				hMax = (r == rings - 1) ? 0 : 1;

			for (int rt = 0; rt <= hMax; rt++)
			{
				for (int p = 0; p <= p_max; p++)
				{
					complex<double> M = M_p_Local[p];
					for (int q = p % 2; q <= min(p, q_max); q += 2)
						//for (int q = p % 2; q <= p; q += 2)
					{
						complex<double> sum_s(0.0);
						for (int s = q; s <= p; s += 2)
						{
							double tmp_s = Beta[p][q][s] * powReal(nwc, s);
							complex<double> sum_t(0.0);
							int sqn = (int)((s - q) / 2.0);
							for (int t = 0; t <= sqn; t++)
							{
								complex<double> tmp_t = (double)bc[sqn][t];
								complex<double> sum_u(0.0);
								int sqnsodd = (int)((s + q) / 2.0);
								for (int u = 0; u <= sqnsodd; u++)
								{
									complex<double> delta(0, 0);
									if (deltasSpeeder[r][rt][t][u] == delta)
									{
										delta = deltaII(Integrals[t][u], x1, y1, (x2 - x1 + 1));
										if (r < rings - 1 && rt == 0)
											delta -= deltaII(Integrals[t][u], x1Inner, y1Inner, abs(x2Inner - x1Inner + 1));
										deltasSpeeder[r][rt][t][u] = delta;
									}
									complex<double> tmp_u = (double)bc[sqnsodd][u] * Speeder[jc2][kc2][sqn - t][sqnsodd - u]; // go back here
									sum_u += tmp_u * deltasSpeeder[r][rt][t][u];
								}
								sum_t += tmp_t * sum_u;
							}
							sum_s += tmp_s * sum_t;
						}
						complex<double> cm = M * sum_s;
						features[++id] = abs(M * sum_s);
					}
				}
			}
		}
		delete[] M_p_Local;

		return make_tuple(featuresCount, features);
	}

	/// <summary>Ekstrachuje cechy z podanych plikow</summary>
	/// <param name = 'paths'>Wektor sciezek do plikow</param>
	virtual tuple<int, int, const double* const*> extractMultipleFeatures(const vector<string> &paths)  override
	{
		const double ** X = new const double*[paths.size()];

#pragma omp parallel for num_threads(OMP_NUM_THR)
		for (int i = 0; i < (int)paths.size(); i++)
		{
			ZernikeII zernike(this);

			auto[fc, fets] = zernike.extractFeatures(paths[i]);
			X[i] = fets;
		}
		return make_tuple((int)paths.size(), featuresCount, X);
	}
};
vector<vector<vector<vector<complex<double>>>>> ZernikeII::Speeder = vector<vector<vector<vector<complex<double>>>>>();
vector<vector<unsigned long long>> ZernikeII::bc = vector<vector<unsigned long long int>>();
vector<vector<vector<long long>>> ZernikeII::Beta = vector<vector<vector<long long>>>();
vector<double> ZernikeII::a = vector<double>();
bool ZernikeII::initialized = false;

class ZernikePII : public ZernikeII
{
private:
	//vector<vector<vector<vector<vector<vector<complex<double>>>>>>> Integrals;
	complex<double> ******Integrals = nullptr;

	void calculateII2(const double* const* img, int d)
	{
		clearImageData();

		int ileS1 = (int)ceil(1.0*width / d);
		int ileS2 = (int)ceil(1.0*height / d);
		int s1 = width;
		int s2 = height;
		int tlim = p_max / 2;
		int tg = (int)ceil((p_max - min(q_max, p_max)) / 2.0);

		Integrals = new complex<double>*****[ileS2];
		for (int iS2 = 0; iS2 < ileS2; iS2++)
		{
			int yS2p = iS2 * d;
			int yS2k = (iS2 + 1)*d - 1;
			if (yS2k >= height)
				yS2k = height - 1;
			Integrals[iS2] = new complex<double>****[ileS1];
			for (int iS1 = 0; iS1 < ileS1; iS1++)
			{
				int xS1p = iS1 * d;
				int xS1k = (iS1 + 1)*d + -1;
				if (xS1k >= width)
					xS1k = width - 1;
				int maxu = p_max - tg;
				Integrals[iS2][iS1] = new complex<double>***[tlim + 1];

				for (int t = 0; t <= tlim; t++)
				{
					if (t > tg)
						maxu = p_max - t;
					Integrals[iS2][iS1][t] = new complex<double>**[maxu + 1];
#pragma omp parallel for num_threads(OMP_NUM_THR)
					for (int u = t; u <= maxu; u++)
					{
						complex<double>* ll = new complex<double>[xS1k - xS1p + 1];
						Integrals[iS2][iS1][t][u] = new complex<double>*[yS2k - yS2p + 1];
						for (int y = yS2p; y <= yS2k; y++)
						{
							Integrals[iS2][iS1][t][u][y - yS2p] = new complex<double>[xS1k - xS1p + 1];
							for (int x = xS1p; x <= xS1k; x++)
							{
								int xx = x - xS1p;
								int yy = y - yS2p;
								complex<double> a = img[y][x] * (powIntQuick(powInt(x + 1, 2) + powInt(y + 1, 2), t)
									* powComQuick(complex<double>(x + 1, y + 1), u - t));
								complex<double> s;
								if (xx > 0)
									s = ll[xx - 1] + a;
								else
									s = a;
								ll[xx] = s;
								if (yy > 0)
									s = s + Integrals[iS2][iS1][t][u][yy - 1][xx];
								Integrals[iS2][iS1][t][u][yy][xx] = s;
							}
						}
						delete[] ll;
					}
				}
			}
		}
		for (int iS2 = 0; iS2 < ileS2; iS2++)
		{
			int yS2p = iS2 * d;
			int yS2k = (iS2 + 1)*d - 1;
			if (yS2k >= height)
				yS2k = height - 1;
			for (int iS1 = 0; iS1 < ileS1; iS1++)
			{
				int xS1p = iS1 * d;
				int xS1k = (iS1 + 1)*d + -1;
				if (xS1k >= width)
					xS1k = width - 1;
				int maxu = p_max - tg;
				for (int t = 0; t <= tlim; t++)
				{
					if (t > tg)
						maxu = p_max - t;
#pragma omp parallel for num_threads(OMP_NUM_THR)
					for (int u = 0; u < t; u++)
					{
						Integrals[iS2][iS1][t][u] = new complex<double>*[yS2k - yS2p + 1];
						for (int y = yS2p; y <= yS2k; y++)
						{
							Integrals[iS2][iS1][t][u][y - yS2p] = new complex<double>[xS1k - xS1p + 1];
							for (int x = xS1p; x <= xS1k; x++)
							{
								int xx = x - xS1p;
								int yy = y - yS2p;
								Integrals[iS2][iS1][t][u][yy][xx] = conj(Integrals[iS2][iS1][u][t][yy][xx]);
							}
						}
					}
				}
			}

		}
	}

	complex<double> deltaII(const complex<double>* const* Integral, const int jp, const int kp, const int Nj, const int Nk) const
	{
		complex<double> delta;

		if (jp == 0 && kp == 0)
			delta = Integral[jp + Nj - 1][kp + Nk - 1];
		else if (jp == 0)
			delta = Integral[jp + Nj - 1][kp + Nk - 1] - Integral[jp + Nj - 1][kp - 1];
		else if (kp == 0)
			delta = Integral[jp + Nj - 1][kp + Nk - 1] - Integral[jp - 1][kp + Nk - 1];
		else
			delta = Integral[jp + Nj - 1][kp + Nk - 1] - Integral[jp + Nj - 1][kp - 1]
			- Integral[jp - 1][kp + Nk - 1] + Integral[jp - 1][kp - 1];
		return delta;
	}

	complex<double> deltaP(const complex<double>* const*const*const*const*const* Integrals, const int d, const int yp, const int xp, const int N, const int t, const int u) const
	{
		complex<double> deltaP = 0;
		int yk = yp + N - 1;
		int xk = xp + N - 1;
		int V[2][4] = { { yp, yp, yk, yk },{ xp, xk, xk, xp } };
		for (int i = 0; i<4; i++)
		{
			V[0][i] = (int)floor(1.0*V[0][i] / d);
			V[1][i] = (int)floor(1.0*V[1][i] / d);
		}
		if (V[0][0] == V[0][1] && V[1][0] == V[1][1] && V[0][0] == V[0][2] && V[1][0] == V[1][2] && V[0][0] == V[0][3] && V[1][0] == V[1][3])
		{
			//wszystkie w jednym calkowym
			//cout << "Wszystkie w jednym" << endl;
			int yy1 = yp - V[0][0] * d;
			int xx1 = xp - V[1][0] * d;
			deltaP = deltaII(Integrals[V[0][0]][V[1][0]][t][u], yy1, xx1, N, N);
		}
		else if (V[0][0] == V[0][3] && V[1][0] == V[1][3] && V[0][2] == V[0][1] && V[1][2] == V[1][1])
		{
			// obok siebie
			//cout << "Obok siebie" << endl;
			int yy1 = yp - V[0][0] * d;
			int xx1 = xp - V[1][0] * d;
			int xx2 = 0;
			deltaP = deltaII(Integrals[V[0][0]][V[1][0]][t][u], yy1, xx1, N, V[1][1] * d - xp) +
				deltaII(Integrals[V[0][1]][V[1][1]][t][u], yy1, xx2, N, xk - V[1][1] * d + 1);
		}
		else if (V[0][0] == V[0][1] && V[1][0] == V[1][1] && V[0][2] == V[0][3] && V[1][2] == V[1][3])
		{
			// jedno nad drugim
			//cout << "Jedno pod drugim" << endl;
			int yy1 = yp - V[0][0] * d;
			int xx1 = xp - V[1][0] * d;
			int yy2 = 0;
			deltaP = deltaII(Integrals[V[0][0]][V[1][0]][t][u], yy1, xx1, V[0][2] * d - yp, N) +
				deltaII(Integrals[V[0][2]][V[1][2]][t][u], yy2, xx1, yk - V[0][2] * d + 1, N);
		}
		else
		{
			//cout << "W czterech" << endl;
			int yy1 = yp - V[0][0] * d;
			int xx1 = xp - V[1][0] * d;
			int Ny1 = V[0][2] * d - yp;
			int Nx1 = V[1][1] * d - xp;
			int Ny2 = yk - V[0][2] * d + 1;
			int Nx2 = xk - V[1][1] * d + 1;
			deltaP = deltaII(Integrals[V[0][0]][V[1][0]][t][u], yy1, xx1, Ny1, Nx1) +
				deltaII(Integrals[V[0][1]][V[1][1]][t][u], yy1, 0, Ny1, Nx2) +
				deltaII(Integrals[V[0][2]][V[1][2]][t][u], 0, 0, Ny2, Nx2) +
				deltaII(Integrals[V[0][3]][V[1][3]][t][u], 0, xx1, Ny2, Nx1);
		}
		return deltaP;
	}

	const int d;

	ZernikePII(ZernikePII * parent) : ZernikeII(parent), d{parent->d}
	{
	}
public:
	using Extractor::extractFromWindow;

	~ZernikePII()
	{
		clearImageData();
	}

	ZernikePII() : ZernikePII(8, 8, 6, 0, 100, SaveFileType::text) {}

	ZernikePII(const int p_max, const int q_max, const int rings, const int ringsType, const int d, SaveFileType fileType = SaveFileType::text) 
		:ZernikeII(p_max, q_max, rings, ringsType, fileType), d{ d }
	{
	}

	static string GetType()
	{
		return "ZernikePiiExtractor";
	}

	/// <summary>Zwraca typ cechy</summary>
	/// <returns>Typ klasyfikatora</returns>
	string getType() const override
	{
		return GetType();
	}

	bool getRectangleWindowsRequirement() const override
	{
		return true;
	}

	/// <summary>Zwraca opis ekstraktora cech</summary>
	/// <returns>Opis ekstraktora cech</returns>
	string toString() const override
	{
		string text = getType() + "\r\n";
		text += "Pmax: " + to_string(p_max) + "\r\n";
		text += "Qmax: " + to_string(q_max) + "\r\n";
		text += "Rings: " + to_string(rings) + "\r\n";
		text += "RingsType: " + to_string(ringsType) + "\r\n";
		text += "Width: " + to_string(d) + "\r\n";

		return text;
	}

	void loadImageData(const string path) override
	{
		clearImageData();
		auto[height, width, img] = loadImage(path, saveMode);
		this->width = width;
		this->height = height;

		int maxDim = max(width, height);

		calculateII2(img, d);

#pragma omp critical (RecalculateSpeeder)
		{
			if ((int)Speeder.size() < maxDim)
				speederUpdate(width);
		}

		for (int i = 0; i < height; i++)
			delete[] img[i];
		delete[] img;
	}

	virtual void loadImageData(const double* const* img, int height, int width) override
	{
		clearImageData();
		this->width = width;
		this->height = height;
		int maxDim = max(width, height);

		calculateII2(img, d);

#pragma omp critical (RecalculateSpeeder)
		{
			if ((int)Speeder.size() < maxDim)
				speederUpdate(maxDim);
		}
	}

	void clearImageData() override
	{
		if (Integrals != nullptr)
		{
			int ileS1 = (int)ceil(1.0*width / d);
			int ileS2 = (int)ceil(1.0*height / d);
			int s1 = width;
			int s2 = height;
			int tlim = p_max / 2;
			int tg = (int)ceil((p_max - min(q_max, p_max)) / 2.0);
			int maxu = p_max - tg;

			for (int iS2 = 0; iS2 < ileS2; iS2++)
			{
				int yS2p = iS2 * d;
				int yS2k = (iS2 + 1)*d - 1;
				if (yS2k >= height)
					yS2k = height - 1;

				for (int iS1 = 0; iS1 < ileS1; iS1++)
				{
					maxu = p_max - tg;
					for (int t = 0; t <= tlim; t++)
					{
						if (t > tg)
							maxu = p_max - t;
						for (int u = 0; u <= maxu; u++)
						{
							for (int y = yS2p; y < yS2k; y++)
							{
								delete[] Integrals[iS2][iS1][t][u][y - yS2p];
							}
							delete[] Integrals[iS2][iS1][t][u];
						}
						delete[] Integrals[iS2][iS1][t];
					}
					delete[] Integrals[iS2][iS1];
				}
				delete[] Integrals[iS2];
			}
			delete[] Integrals;
		}
		Integrals = nullptr;
	}

	void initializeExtractor(Point* sizes, int sca) override
	{
		if (!initialized)
			ZernikeII::initializeExtractor();

		for (int s1 = 0; s1 < sca; s1++)
		{
			int Wx = sizes[s1].wx;

			if (M_p_w.count(Wx) == 0)
			{
				this->M_p_w[Wx] = new complex<double>[p_max + 1];
				for (int p = 0; p <= p_max; p++)
					this->M_p_w[Wx][p] = (2.0 / pow(Wx, 2)) / (pi2 * a[p]);
			}
		}
	}

	virtual int extractFromWindowSmall(double* features, const int* featuresID, int fLength, int Wx, int Wy, int kp = 0, int jp = 0) 
	{
		complex<double>* M_p = M_p_w[Wx];

		//return extractFromWindow(Wx, Wy, xp, yp);
		double jc = jp + 1 + (Wx - 1) / 2.0;
		double kc = kp + 1 + (Wx - 1) / 2.0;
		int N = Wx;

		int jc2 = (int)round(jc - 0.5);
		int kc2 = (int)round(kc - 0.5);

		vector<int> x1I(rings), y1I(rings), x2I(rings), y2I(rings);
		vector<int> x1_t(rings), y1_t(rings), x2_t(rings), y2_t(rings);
		for (int r = 0; r < rings; r++)
		{
			int WInner = (int)round(N * sqrt((rings - (r + 1)) / (double)rings));
			if (WInner % 2 == 1)
				WInner++;
			x1I[r] = (int)(jc - 0.5 - WInner / 2);
			y1I[r] = (int)(kc - 0.5 - WInner / 2);
			x2I[r] = x1I[r] + WInner - 1;
			y2I[r] = y1I[r] + WInner - 1;

			int wc = (int)round(N*sqrt((rings - r) / (double)rings));
			if (wc % 2 == 1)
				wc++;
			x1_t[r] = (int)(jc - 0.5 - wc / 2);
			y1_t[r] = (int)(kc - 0.5 - wc / 2);

			x2_t[r] = x1_t[r] + wc - 1;
			y2_t[r] = y1_t[r] + wc - 1;
		}

		for (int f = 0; f < fLength; f++)
		{
			int id = featuresID[f];
			const int &p = descriptions[id].p;
			const int &q = descriptions[id].q;
			const int &r = descriptions[id].r;
			const int &rt = descriptions[id].rt;

			double nwc = sqrt(2) / ((double)N);
			int x1Inner = 0, x2Inner = 0, y1Inner = 0, y2Inner = 0;
			if (r < rings - 1)
			{
				x1Inner = x1I[r];
				y1Inner = y1I[r];
				x2Inner = x2I[r];
				y2Inner = y2I[r];
			}

			complex<double> M = 0;
			for (int s = q; s <= p; s += 2)
			{
				double tmp_s = Beta[p][q][s] * powReal(nwc, s);
				complex<double> sum_t;
				int sqn = (int)((s - q) / 2.0);
				for (int t = 0; t <= sqn; t++)
				{
					complex<double> tmp_t = (double)bc[sqn][t];
					complex<double> sum_u;
					int sqnsodd = (int)((s + q) / 2.0);
					for (int u = 0; u <= sqnsodd; u++)
					{
						complex<double> delta(0, 0);
						//if (deltasSpeeder[rt][r][t][u] == delta)
						//{
							delta = deltaP(Integrals, d, x1_t[r], y1_t[r], (x2_t[r] - x1_t[r] + 1), t, u);
							if (r < rings - 1 && rt == 0)
								delta -= deltaP(Integrals, d, x1Inner, y1Inner, abs(x2Inner - x1Inner + 1), t, u);
							//deltasSpeeder[rt][r][t][u] = delta;
						//}
						complex<double> tmp_u = (double)bc[sqnsodd][u] * Speeder[jc2][kc2][sqn - t][sqnsodd - u]; // go back here
						sum_u += tmp_u * delta;
					}
					sum_t += tmp_t * sum_u;
				}
				M += tmp_s * sum_t;
			}
			M *= M_p[p];

			features[id] = abs(M);
		}
		return featuresCount;
	}

	/// <param name = 'featuresID'>Numery cech</param>
	/// <param name = 'Wx'>Szerokosc okna</param>
	/// <param name = 'Wy'>Wysokosc okna</param>
	/// <param name = 'xp'>X-pozycja okna (lewy gorny rog)</param>
	/// <param name = 'yp'>Y-pozycja okna (lewy gorny rog)</param>
	virtual int extractFromWindow(double* features, const int* featuresID, int fLength, int Wx, int Wy, int kp = 0, int jp = 0) override
	{
		if (fLength < 30)
			return extractFromWindowSmall(features, featuresID, fLength, Wx, Wy, kp, jp);
		else
		{
			complex<double>* M_p = M_p_w[Wx];

			//return extractFromWindow(Wx, Wy, xp, yp);
			double jc = jp + 1 + (Wx - 1) / 2.0;
			double kc = kp + 1 + (Wx - 1) / 2.0;
			int N = Wx;

			vector<vector<vector<vector<complex<double>>>>> deltasSpeeder(ringsType + 1,
				vector<vector<vector<complex<double>>>>(rings,
					vector<vector<complex<double>>>((p_max + q_max + 2) / 2,
						vector<complex<double>>((p_max + q_max + 2) / 2))));

			int jc2 = (int)round(jc - 0.5);
			int kc2 = (int)round(kc - 0.5);

			vector<int> x1I(rings), y1I(rings), x2I(rings), y2I(rings);
			vector<int> x1_t(rings), y1_t(rings), x2_t(rings), y2_t(rings);
			for (int r = 0; r < rings; r++)
			{
				int WInner = (int)round(N * sqrt((rings - (r + 1)) / (double)rings));
				if (WInner % 2 == 1)
					WInner++;
				x1I[r] = (int)(jc - 0.5 - WInner / 2);
				y1I[r] = (int)(kc - 0.5 - WInner / 2);
				x2I[r] = x1I[r] + WInner - 1;
				y2I[r] = y1I[r] + WInner - 1;

				int wc = (int)round(N*sqrt((rings - r) / (double)rings));
				if (wc % 2 == 1)
					wc++;
				x1_t[r] = (int)(jc - 0.5 - wc / 2);
				y1_t[r] = (int)(kc - 0.5 - wc / 2);

				x2_t[r] = x1_t[r] + wc - 1;
				y2_t[r] = y1_t[r] + wc - 1;
			}

			for (int f = 0; f < fLength; f++)
			{
				int id = featuresID[f];
				const int &p = descriptions[id].p;
				const int &q = descriptions[id].q;
				const int &r = descriptions[id].r;
				const int &rt = descriptions[id].rt;

				double nwc = sqrt(2) / ((double)N);
				int x1Inner = 0, x2Inner = 0, y1Inner = 0, y2Inner = 0;
				if (r < rings - 1)
				{
					x1Inner = x1I[r];
					y1Inner = y1I[r];
					x2Inner = x2I[r];
					y2Inner = y2I[r];
				}

				complex<double> M = 0;
				for (int s = q; s <= p; s += 2)
				{
					double tmp_s = Beta[p][q][s] * powReal(nwc, s);
					complex<double> sum_t;
					int sqn = (int)((s - q) / 2.0);
					for (int t = 0; t <= sqn; t++)
					{
						complex<double> tmp_t = (double)bc[sqn][t];
						complex<double> sum_u;
						int sqnsodd = (int)((s + q) / 2.0);
						for (int u = 0; u <= sqnsodd; u++)
						{
							complex<double> delta(0, 0);
							if (deltasSpeeder[rt][r][t][u] == delta)
							{
								delta = deltaP(Integrals, d, x1_t[r], y1_t[r], (x2_t[r] - x1_t[r] + 1), t, u);
								if (r < rings - 1 && rt == 0)
									delta -= deltaP(Integrals, d, x1Inner, y1Inner, abs(x2Inner - x1Inner + 1), t, u);
								deltasSpeeder[rt][r][t][u] = delta;
							}
							complex<double> tmp_u = (double)bc[sqnsodd][u] * Speeder[jc2][kc2][sqn - t][sqnsodd - u]; // go back here
							sum_u += tmp_u * deltasSpeeder[rt][r][t][u];
						}
						sum_t += tmp_t * sum_u;
					}
					M += tmp_s * sum_t;
				}
				M *= M_p[p];

				features[id] = abs(M);
			}
			return featuresCount;
		}
	}

	/// <summary>Ekstrachuje cechy z podanego obrazu</summary>
	/// <param name = 'Wx'>Szerokosc okna</param>
	/// <param name = 'Wy'>Wysokosc okna</param>
	/// <param name = 'xp'>X-pozycja okna (lewy gorny rog)</param>
	/// <param name = 'yp'>Y-pozycja okna (lewy gorny rog)</param>
	/// <returns>Ekstrachowane cechy</returns>
	virtual tuple<int, const double*> extractFromWindow(int Wx, int Wy, int kp = 0, int jp = 0) override
	{
		double* features = new double[featuresCount];
		int id = -1;

		double npow2Local = (2.0 / pow(Wx, 2));
		double* M_p_Local = new double[p_max + 1];
		for (int p = 0; p <= p_max; p++)
			M_p_Local[p] = npow2Local / (pi2 * a[p]);

		vector<double> F;
		double jc = jp + 1 + (Wx - 1) / 2.0;
		double kc = kp + 1 + (Wx - 1) / 2.0;
		int N = Wx;

		int jc2 = (int)round(jc - 0.5);
		int kc2 = (int)round(kc - 0.5);

		vector<vector<vector<vector<complex<double>>>>> deltasSpeeder(rings,
			vector<vector<vector<complex<double>>>>(ringsType + 1,
				vector<vector<complex<double>>>((p_max + q_max + 2) / 2,
					vector<complex<double>>((p_max + q_max + 2) / 2))));

		for (int r = 0; r < rings; r++)
		{
			int wc = (int)round(N*sqrt((rings - r) / (double)rings));
			if (wc % 2 == 1)
				wc++;
			int x1 = (int)(jc - 0.5 - wc / 2);
			int y1 = (int)(kc - 0.5 - wc / 2);

			int x2 = x1 + wc - 1;
			int y2 = y1 + wc - 1;

			double nwc = sqrt(2) / ((double)N);
			int x1Inner = 0, x2Inner = 0, y1Inner = 0, y2Inner = 0, WInner = 0;
			if (r < rings - 1)
			{
				WInner = (int)round(N * sqrt((rings - (r + 1)) / (double)rings));
				if (WInner % 2 == 1)
					WInner++;
				x1Inner = (int)(jc - 0.5 - WInner / 2);
				y1Inner = (int)(kc - 0.5 - WInner / 2);
				x2Inner = x1Inner + WInner - 1;
				y2Inner = y1Inner + WInner - 1;
			}

			int hMax = 0;
			if (ringsType == 1)
				hMax = (r == rings - 1) ? 0 : 1;

			for (int rt = 0; rt <= hMax; rt++)
			{
				for (int p = 0; p <= p_max; p++)
				{
					complex<double> M = M_p_Local[p];
					for (int q = p % 2; q <= min(p, q_max); q += 2)
					//for (int q = p % 2; q <= p; q += 2)
					{
						complex<double> sum_s(0.0);
						for (int s = q; s <= p; s += 2)
						{
							double tmp_s = Beta[p][q][s] * powReal(nwc, s);
							complex<double> sum_t(0.0);
							int sqn = (int)((s - q) / 2.0);
							for (int t = 0; t <= sqn; t++)
							{
								complex<double> tmp_t = (double)bc[sqn][t];
								complex<double> sum_u(0.0);
								int sqnsodd = (int)((s + q) / 2.0);
								for (int u = 0; u <= sqnsodd; u++)
								{
									complex<double> delta(0, 0);
									if (deltasSpeeder[r][rt][t][u] == delta)
									{
										delta = deltaP(Integrals, d, x1, y1, (x2 - x1 + 1), t, u);
										if (r < rings - 1 && rt == 0)
											delta -= deltaP(Integrals, d, x1Inner, y1Inner, abs(x2Inner - x1Inner + 1), t, u);
										deltasSpeeder[r][rt][t][u] = delta;
									}
									complex<double> tmp_u = (double)bc[sqnsodd][u] * Speeder[jc2][kc2][sqn - t][sqnsodd - u]; // go back here
									sum_u += tmp_u * deltasSpeeder[r][rt][t][u];
								}
								sum_t += tmp_t * sum_u;
							}
							sum_s += tmp_s * sum_t;
						}
						complex<double> cm = M * sum_s;
						features[++id] = abs(M * sum_s);
					}
				}
			}
		}
		delete[] M_p_Local;

		return make_tuple(featuresCount, features);
	}

	/// <summary>Ekstrachuje cechy z podanych plikow</summary>
	/// <param name = 'paths'>Wektor sciezek do plikow</param>
	virtual tuple<int, int, const double* const*> extractMultipleFeatures(const vector<string> &paths)  override
	{
		const double ** X = new const double*[paths.size()];

		#pragma omp parallel for num_threads(OMP_NUM_THR)
		for (int i = 0; i < (int)paths.size(); i++)
		{
			ZernikePII zernike(this);

			auto[fc, fets] = zernike.extractFeatures(paths[i]);
			X[i] = fets;
		}
		return make_tuple((int)paths.size(), featuresCount, X);
	}
};

class ZernikeZII : public ZernikeII
{
private:
	//vector<vector<vector<vector<vector<vector<complex<double>>>>>>> Integrals;
	complex<double> ******Integrals = nullptr;

	void calculateII2(const double* const* img, int w_max, int d)
	{
		clearImageData();

		int ileS1 = (int)ceil(1.0*width / d);
		int ileS2 = (int)ceil(1.0*height / d);
		int s1 = width;
		int s2 = height;
		int tlim = p_max / 2;
		int tg = (int)ceil((p_max - min(q_max, p_max)) / 2.0);

		Integrals = new complex<double>*****[ileS2];
		for (int iS2 = 0; iS2 < ileS2; iS2++)
		{
			int yS2p = iS2 * d;
			int yS2k = (iS2 + 1)*d + w_max - 1;
			if (yS2k >= height)
				yS2k = height - 1;
			Integrals[iS2] = new complex<double>****[ileS1];
			for (int iS1 = 0; iS1 < ileS1; iS1++)
			{
				int xS1p = iS1 * d;
				int xS1k = (iS1 + 1)*d + w_max - 1;
				if (xS1k >= width)
					xS1k = width - 1;
				int maxu = p_max - tg;
				Integrals[iS2][iS1] = new complex<double>***[tlim + 1];
				for (int t = 0; t <= tlim; t++)
				{
					if (t > tg)
						maxu = p_max - t;
					Integrals[iS2][iS1][t] = new complex<double>**[maxu + 1];
#pragma omp parallel for num_threads(OMP_NUM_THR)
					for (int u = t; u <= maxu; u++)
					{
						complex<double>* ll = new complex<double>[d + w_max];
						Integrals[iS2][iS1][t][u] = new complex<double>*[yS2k - yS2p + 1];
						for (int y = yS2p; y <= yS2k; y++)
						{
							Integrals[iS2][iS1][t][u][y - yS2p] = new complex<double>[xS1k - xS1p + 1];
							for (int x = xS1p; x <= xS1k; x++)
							{
								int xx = x - xS1p;
								int yy = y - yS2p;

								complex<double> a = img[y][x] * (powIntQuick(powInt(x + 1, 2) + powInt(y + 1, 2), t)
									* powComQuick(complex<double>(x + 1, y + 1), u - t));
								complex<double> s;
								if (xx > 0)
									s = ll[xx - 1] + a;
								else
									s = a;
								ll[xx] = s;
								if (yy > 0)
									s = s + Integrals[iS2][iS1][t][u][yy - 1][xx];
								Integrals[iS2][iS1][t][u][yy][xx] = s;
							}
						}
						delete[] ll;
					}
				}
			}
		}
		for (int iS2 = 0; iS2 < ileS2; iS2++)
		{
			int yS2p = iS2 * d;
			int yS2k = (iS2 + 1)*d + w_max - 1;
			if (yS2k >= height)
				yS2k = height - 1;
			for (int iS1 = 0; iS1 < ileS1; iS1++)
			{
				int xS1p = iS1 * d;
				int xS1k = (iS1 + 1)*d + w_max - 1;
				if (xS1k >= width)
					xS1k = width - 1;
				int maxu = p_max - tg;
				for (int t = 0; t <= tlim; t++)
				{
					if (t > tg)
						maxu = p_max - t;
#pragma omp parallel for num_threads(OMP_NUM_THR)
					for (int u = 0; u < t; u++)
					{
						Integrals[iS2][iS1][t][u] = new complex<double>*[yS2k - yS2p + 1];
						for (int y = yS2p; y <= yS2k; y++)
						{
							Integrals[iS2][iS1][t][u][y - yS2p] = new complex<double>[xS1k - xS1p + 1];
							for (int x = xS1p; x <= xS1k; x++)
							{
								int xx = x - xS1p;
								int yy = y - yS2p;
								Integrals[iS2][iS1][t][u][yy][xx] = conj(Integrals[iS2][iS1][u][t][yy][xx]);
							}
						}
					}
				}
			}
		}
	}

	complex<double> deltaII(const complex<double>*const*Integral, const int jp, const int kp, const int N) const
	{
		complex<double> delta;

		if (jp == 0 && kp == 0)
			delta = Integral[jp + N - 1][kp + N - 1];
		else if (jp == 0)
			delta = Integral[jp + N - 1][kp + N - 1] - Integral[jp + N - 1][kp - 1];
		else if (kp == 0)
			delta = Integral[jp + N - 1][kp + N - 1] - Integral[jp - 1][kp + N - 1];
		else
			delta = Integral[jp + N - 1][kp + N - 1] - Integral[jp + N - 1][kp - 1]
			- Integral[jp - 1][kp + N - 1] + Integral[jp - 1][kp - 1];
		return delta;
	}

	const int d;
	const int w_max;

	ZernikeZII(ZernikeZII * parent) : ZernikeII(parent), d{ parent->d }, w_max{ parent->w_max}
	{
	}
public:
	using Extractor::extractFromWindow;

	~ZernikeZII()
	{
		clearImageData();
	}

	ZernikeZII() : ZernikeZII(8, 8, 6, 0, 100, 50, SaveFileType::text) {}

	ZernikeZII(const int p_max, const int q_max, const int rings, const int ringsType, const int d, const int w_max, SaveFileType fileType = SaveFileType::text)
		: ZernikeII(p_max, q_max, rings, ringsType, fileType), d{ d }, w_max{ w_max }
	{
	}

	static string GetType()
	{
		return "ZernikeZiiExtractor";
	}

	/// <summary>Zwraca typ cechy</summary>
	/// <returns>Typ klasyfikatora</returns>
	string getType() const override
	{
		return GetType();
	}

	bool getRectangleWindowsRequirement() const override
	{
		return true;
	}

	/// <summary>Zwraca opis ekstraktora cech</summary>
	/// <returns>Opis ekstraktora cech</returns>
	string toString() const override
	{
		string text = getType() + "\r\n";
		text += "Pmax: " + to_string(p_max) + "\r\n";
		text += "Qmax: " + to_string(q_max) + "\r\n";
		text += "Rings: " + to_string(rings) + "\r\n";
		text += "RingsType: " + to_string(ringsType) + "\r\n";
		text += "Width: " + to_string(d) + "\r\n";
		text += "Overlap: " + to_string(w_max) + "\r\n";

		return text;
	}

	virtual void loadImageData(const string path) override
	{
		clearImageData();
		auto[height, width, img] = loadImage(path, saveMode);
		this->width = width;
		this->height = height;

		int maxDim = max(width, height);

		calculateII2(img, w_max, d);

#pragma omp critical (RecalculateSpeeder)
		{
			if ((int)Speeder.size() < maxDim)
				speederUpdate(width);
		}

		for (int i = 0; i < height; i++)
			delete[] img[i];
		delete[] img;
	}

	virtual void loadImageData(const double* const* img, int height, int width) override
	{
		clearImageData();
		this->width = width;
		this->height = height;
		int maxDim = max(width, height);

		calculateII2(img, w_max, d);

#pragma omp critical (RecalculateSpeeder)
		{
			if ((int)Speeder.size() < maxDim)
				speederUpdate(maxDim);
		}
	}

	void clearImageData() override
	{
		if (Integrals != nullptr)
		{
			int ileS1 = (int)ceil(1.0*width / d);
			int ileS2 = (int)ceil(1.0*height / d);
			int s1 = width;
			int s2 = height;
			int tlim = p_max / 2;
			int tg = (int)ceil((p_max - min(q_max, p_max)) / 2.0);
			int maxu = p_max - tg;

			for (int iS2 = 0; iS2 < ileS2; iS2++)
			{
				int yS2p = iS2 * d;
				int yS2k = (iS2 + 1)*d + w_max- 1;
				if (yS2k >= height)
					yS2k = height - 1;

				for (int iS1 = 0; iS1 < ileS1; iS1++)
				{
					maxu = p_max - tg;
					for (int t = 0; t <= tlim; t++)
					{
						if (t > tg)
							maxu = p_max - t;
						for (int u = 0; u <= maxu; u++)
						{
							for (int y = yS2p; y < yS2k; y++)
							{
								delete[] Integrals[iS2][iS1][t][u][y - yS2p];
							}
							delete[] Integrals[iS2][iS1][t][u];
						}
						delete[] Integrals[iS2][iS1][t];
					}
					delete[] Integrals[iS2][iS1];
				}
				delete[] Integrals[iS2];
			}
			delete[] Integrals;
		}
		Integrals = nullptr;
	}

	void initializeExtractor(Point* sizes, int sca) override
	{
		if (!initialized)
			ZernikeII::initializeExtractor();

		for (int s1 = 0; s1 < sca; s1++)
		{
			int Wx = sizes[s1].wx;

			if (M_p_w.count(Wx) == 0)
			{
				this->M_p_w[Wx] = new complex<double>[p_max + 1];
				for (int p = 0; p <= p_max; p++)
					this->M_p_w[Wx][p] = (2.0 / pow(Wx, 2)) / (pi2 * a[p]);
			}
		}
	}

	virtual int extractFromWindowSmall(double* features, const int* featuresID, int fLength, int Wx, int Wy, int kp = 0, int jp = 0)
	{
		complex<double>* M_p = M_p_w[Wx];

		//return extractFromWindow(Wx, Wy, xp, yp);
		double jc = jp + 1 + (Wx - 1) / 2.0;
		double kc = kp + 1 + (Wx - 1) / 2.0;
		int N = Wx;


		int jc2 = (int)round(jc - 0.5);
		int kc2 = (int)round(kc - 0.5);

		vector<int> x1I(rings), y1I(rings), x2I(rings), y2I(rings);
		vector<int> x1_t(rings), y1_t(rings), x2_t(rings), y2_t(rings);
		for (int r = 0; r < rings; r++)
		{
			int WInner = (int)round(N * sqrt((rings - (r + 1)) / (double)rings));
			if (WInner % 2 == 1)
				WInner++;
			x1I[r] = (int)(jc - 0.5 - WInner / 2);
			y1I[r] = (int)(kc - 0.5 - WInner / 2);
			x2I[r] = x1I[r] + WInner - 1;
			y2I[r] = y1I[r] + WInner - 1;

			int wc = (int)round(N*sqrt((rings - r) / (double)rings));
			if (wc % 2 == 1)
				wc++;
			x1_t[r] = (int)(jc - 0.5 - wc / 2);
			y1_t[r] = (int)(kc - 0.5 - wc / 2);

			x2_t[r] = x1_t[r] + wc - 1;
			y2_t[r] = y1_t[r] + wc - 1;
		}

		for (int f = 0; f < fLength; f++)
		{
			int id = featuresID[f];
			const int &p = descriptions[id].p;
			const int &q = descriptions[id].q;
			const int &r = descriptions[id].r;
			const int &rt = descriptions[id].rt;

			double nwc = sqrt(2) / ((double)N);
			int x1Inner = 0, x2Inner = 0, y1Inner = 0, y2Inner = 0;
			if (r < rings - 1)
			{
				x1Inner = x1I[r];
				y1Inner = y1I[r];
				x2Inner = x2I[r];
				y2Inner = y2I[r];
			}

			complex<double> M = 0;
			for (int s = q; s <= p; s += 2)
			{
				double tmp_s = Beta[p][q][s] * powReal(nwc, s);
				complex<double> sum_t;
				int sqn = (int)((s - q) / 2.0);
				for (int t = 0; t <= sqn; t++)
				{
					complex<double> tmp_t = (double)bc[sqn][t];
					complex<double> sum_u;
					int sqnsodd = (int)((s + q) / 2.0);
					for (int u = 0; u <= sqnsodd; u++)
					{
						complex<double> delta(0, 0);
						//if (deltasSpeeder[rt][r][t][u] == delta)
						//{
							int II_y = (int)floor(1.0 * x1_t[r] / d);
							int II_x = (int)floor(1.0 * y1_t[r] / d);
							delta = deltaII(Integrals[II_y][II_x][t][u], x1_t[r] - II_y * d, y1_t[r] - II_x * d, (x2_t[r] - x1_t[r] + 1));
							if (r < rings - 1 && rt == 0)
								delta -= deltaII(Integrals[II_y][II_x][t][u], x1Inner - II_y * d, y1Inner - II_x * d, abs(x2Inner - x1Inner + 1));
						//	deltasSpeeder[rt][r][t][u] = delta;
						//}
						complex<double> tmp_u = (double)bc[sqnsodd][u] * Speeder[jc2][kc2][sqn - t][sqnsodd - u]; // go back here
						sum_u += tmp_u * delta;//deltasSpeeder[rt][r][t][u];
					}
					sum_t += tmp_t * sum_u;
				}
				M += tmp_s * sum_t;
			}
			M *= M_p[p];
			features[id] = abs(M);
		}
		return featuresCount;
	}

	/// <param name = 'featuresID'>Numery cech</param>
	/// <param name = 'Wx'>Szerokosc okna</param>
	/// <param name = 'Wy'>Wysokosc okna</param>
	/// <param name = 'xp'>X-pozycja okna (lewy gorny rog)</param>
	/// <param name = 'yp'>Y-pozycja okna (lewy gorny rog)</param>
	virtual int extractFromWindow(double* features, const int* featuresID, int fLength, int Wx, int Wy, int kp = 0, int jp = 0) override
	{
		if (fLength < 30)
			return extractFromWindowSmall(features, featuresID, fLength, Wx, Wy, kp, jp);
		else
		{
			complex<double>* M_p = M_p_w[Wx];

			//return extractFromWindow(Wx, Wy, xp, yp);
			double jc = jp + 1 + (Wx - 1) / 2.0;
			double kc = kp + 1 + (Wx - 1) / 2.0;
			int N = Wx;

			vector<vector<vector<vector<complex<double>>>>> deltasSpeeder(ringsType + 1,
				vector<vector<vector<complex<double>>>>(rings,
					vector<vector<complex<double>>>((p_max + q_max + 2) / 2,
						vector<complex<double>>((p_max + q_max + 2) / 2))));

			int jc2 = (int)round(jc - 0.5);
			int kc2 = (int)round(kc - 0.5);

			vector<int> x1I(rings), y1I(rings), x2I(rings), y2I(rings);
			vector<int> x1_t(rings), y1_t(rings), x2_t(rings), y2_t(rings);
			for (int r = 0; r < rings; r++)
			{
				int WInner = (int)round(N * sqrt((rings - (r + 1)) / (double)rings));
				if (WInner % 2 == 1)
					WInner++;
				x1I[r] = (int)(jc - 0.5 - WInner / 2);
				y1I[r] = (int)(kc - 0.5 - WInner / 2);
				x2I[r] = x1I[r] + WInner - 1;
				y2I[r] = y1I[r] + WInner - 1;

				int wc = (int)round(N*sqrt((rings - r) / (double)rings));
				if (wc % 2 == 1)
					wc++;
				x1_t[r] = (int)(jc - 0.5 - wc / 2);
				y1_t[r] = (int)(kc - 0.5 - wc / 2);

				x2_t[r] = x1_t[r] + wc - 1;
				y2_t[r] = y1_t[r] + wc - 1;
			}

			for (int f = 0; f < fLength; f++)
			{
				int id = featuresID[f];
				const int &p = descriptions[id].p;
				const int &q = descriptions[id].q;
				const int &r = descriptions[id].r;
				const int &rt = descriptions[id].rt;

				double nwc = sqrt(2) / ((double)N);
				int x1Inner = 0, x2Inner = 0, y1Inner = 0, y2Inner = 0;
				if (r < rings - 1)
				{
					x1Inner = x1I[r];
					y1Inner = y1I[r];
					x2Inner = x2I[r];
					y2Inner = y2I[r];
				}

				complex<double> M = 0;
				for (int s = q; s <= p; s += 2)
				{
					double tmp_s = Beta[p][q][s] * powReal(nwc, s);
					complex<double> sum_t;
					int sqn = (int)((s - q) / 2.0);
					for (int t = 0; t <= sqn; t++)
					{
						complex<double> tmp_t = (double)bc[sqn][t];
						complex<double> sum_u;
						int sqnsodd = (int)((s + q) / 2.0);
						for (int u = 0; u <= sqnsodd; u++)
						{
							complex<double> delta(0, 0);
							if (deltasSpeeder[rt][r][t][u] == delta)
							{
								int II_y = (int)floor(1.0 * x1_t[r] / d);
								int II_x = (int)floor(1.0 * y1_t[r] / d);
								delta = deltaII(Integrals[II_y][II_x][t][u], x1_t[r] - II_y * d, y1_t[r] - II_x * d, (x2_t[r] - x1_t[r] + 1));
								if (r < rings - 1 && rt == 0)
									delta -= deltaII(Integrals[II_y][II_x][t][u], x1Inner - II_y * d, y1Inner - II_x * d, abs(x2Inner - x1Inner + 1));
								deltasSpeeder[rt][r][t][u] = delta;
							}
							complex<double> tmp_u = (double)bc[sqnsodd][u] * Speeder[jc2][kc2][sqn - t][sqnsodd - u]; // go back here
							sum_u += tmp_u * deltasSpeeder[rt][r][t][u];
						}
						sum_t += tmp_t * sum_u;
					}
					M += tmp_s * sum_t;
				}
				M *= M_p[p];
				features[id] = abs(M);
			}
			return featuresCount;
		}
	}

	/// <summary>Ekstrachuje cechy z podanego obrazu</summary>
	/// <param name = 'Wx'>Szerokosc okna</param>
	/// <param name = 'Wy'>Wysokosc okna</param>
	/// <param name = 'xp'>X-pozycja okna (lewy gorny rog)</param>
	/// <param name = 'yp'>Y-pozycja okna (lewy gorny rog)</param>
	/// <returns>Ekstrachowane cechy</returns>
	virtual tuple<int, const double*> extractFromWindow(int Wx, int Wy, int kp = 0, int jp = 0) override
	{
		double* features = new double[featuresCount];
		int id = -1;

		double npow2Local = (2.0 / pow(Wx, 2));
		double* M_p_Local = new double[p_max + 1];
		for (int p = 0; p <= p_max; p++)
			M_p_Local[p] = npow2Local / (pi2 * a[p]);

		vector<double> F;
		double jc = jp + 1 + (Wx - 1) / 2.0;
		double kc = kp + 1 + (Wx - 1) / 2.0;
		int N = Wx;

		int jc2 = (int)round(jc - 0.5);
		int kc2 = (int)round(kc - 0.5);

		vector<vector<vector<vector<complex<double>>>>> deltasSpeeder(rings,
			vector<vector<vector<complex<double>>>>(ringsType + 1,
				vector<vector<complex<double>>>((p_max + q_max + 2) / 2,
					vector<complex<double>>((p_max + q_max + 2) / 2))));

		for (int r = 0; r < rings; r++)
		{
			int wc = (int)round(N*sqrt((rings - r) / (double)rings));
			if (wc % 2 == 1)
				wc++;
			int x1 = (int)(jc - 0.5 - wc / 2);
			int y1 = (int)(kc - 0.5 - wc / 2);

			int x2 = x1 + wc - 1;
			int y2 = y1 + wc - 1;

			double nwc = sqrt(2) / ((double)N);
			int x1Inner = 0, x2Inner = 0, y1Inner = 0, y2Inner = 0, WInner = 0;
			if (r < rings - 1)
			{
				WInner = (int)round(N * sqrt((rings - (r + 1)) / (double)rings));
				if (WInner % 2 == 1)
					WInner++;
				x1Inner = (int)(jc - 0.5 - WInner / 2);
				y1Inner = (int)(kc - 0.5 - WInner / 2);
				x2Inner = x1Inner + WInner - 1;
				y2Inner = y1Inner + WInner - 1;
			}

			int hMax = 0;
			if (ringsType == 1)
				hMax = (r == rings - 1) ? 0 : 1;

			for (int rt = 0; rt <= hMax; rt++)
			{
				for (int p = 0; p <= p_max; p++)
				{
					complex<double> M = M_p_Local[p];
					//for (int q = p % 2; q <= p; q += 2)
					for (int q = p % 2; q <= min(p, q_max); q += 2)
					{
						complex<double> sum_s(0.0);
						for (int s = q; s <= p; s += 2)
						{
							double tmp_s = Beta[p][q][s] * powReal(nwc, s);
							complex<double> sum_t(0.0);
							int sqn = (int)((s - q) / 2.0);
							for (int t = 0; t <= sqn; t++)
							{
								complex<double> tmp_t = (double)bc[sqn][t];
								complex<double> sum_u(0.0);
								int sqnsodd = (int)((s + q) / 2.0);
								for (int u = 0; u <= sqnsodd; u++)
								{
									complex<double> delta(0, 0);
									if (deltasSpeeder[r][rt][t][u] == delta)
									{
										int II_y = (int)floor(1.0*x1 / d);
										int II_x = (int)floor(1.0*y1 / d);
										delta = deltaII(Integrals[II_y][II_x][t][u], x1 - II_y * d, y1 - II_x * d, (x2 - x1 + 1));
										if (r < rings - 1 && rt == 0)
											delta -= deltaII(Integrals[II_y][II_x][t][u], x1Inner - II_y * d, y1Inner - II_x * d, abs(x2Inner - x1Inner + 1));
										deltasSpeeder[r][rt][t][u] = delta;
									}
									complex<double> tmp_u = (double)bc[sqnsodd][u] * Speeder[jc2][kc2][sqn - t][sqnsodd - u]; // go back here
									sum_u += tmp_u * deltasSpeeder[r][rt][t][u];
								}
								sum_t += tmp_t * sum_u;
							}
							sum_s += tmp_s * sum_t;
						}
						complex<double> cm = M * sum_s;
						features[++id] = abs(M * sum_s);
					}
				}
			}
		}
		delete[] M_p_Local;

		return make_tuple(featuresCount, features);
	}

	/// <summary>Ekstrachuje cechy z podanych plikow</summary>
	/// <param name = 'paths'>Wektor sciezek do plikow</param>
	virtual tuple<int, int, const double* const*> extractMultipleFeatures(const vector<string> &paths)  override
	{
		const double ** X = new const double*[paths.size()];

		#pragma omp parallel for num_threads(OMP_NUM_THR)
		for (int i = 0; i < (int)paths.size(); i++)
		{
			ZernikeZII zernike(this);

			auto[fc, fets] = zernike.extractFeatures(paths[i]);
			X[i] = fets;
		}
		return make_tuple((int)paths.size(), featuresCount, X);
	}
};

class ZernikeIIinvariants : public ZernikeII
{
private:
	struct FeatureDescriptor
	{
		int r;
		int rt;
		int k;
		int p;
		int q;
		int v;
		int s;
		int kind;
	};

	vector<FeatureDescriptor> descriptions;
	const int d;

	ZernikeIIinvariants(ZernikeIIinvariants * parent) : ZernikeII(parent), d{ parent->d }, descriptions(parent->descriptions)
	{
		this->featuresCount = parent->featuresCount;
	}
public:
	using Extractor::extractFromWindow;

	~ZernikeIIinvariants()
	{
		clearImageData();
	}

	ZernikeIIinvariants() : ZernikeIIinvariants(8, 8, 6, 0, 100, SaveFileType::text) {}

	ZernikeIIinvariants(const int p_max, const int q_max, const int rings, const int ringsType, const int d, SaveFileType fileType = SaveFileType::text)
		:ZernikeII(p_max, q_max, rings, ringsType, fileType), d{ d }
	{
		for (int r = 0; r < rings; r++)
		{
			int hMax = 0;
			if (ringsType == 1)
				hMax = (r == rings - 1) ? 0 : 1;

			for (int rt = 0; rt <= hMax; rt++)
			{
				descriptions.push_back(FeatureDescriptor{ r, rt, 0, 0, 0, 0, 0, 0 });

				for (int k = 1; k <= p_max; k++)
				{
					int q_min = 1;
					if (k == 1)  q_min = 0;
					for (int q = q_min; q <= q_max; q++)
					{
						int s = k * q;
						for (int p = q; p <= p_max; p += 2)
						{
							int v_min = s;
							if (k == 1)
							{
								v_min = p;
								if (q == 0)
									v_min = p + 2;
							}
							for (int v = v_min; v <= q_max; v += 2)
							{
								descriptions.push_back(FeatureDescriptor{ r, rt, k, p, q, v, s, 0 });
								if (!((k == 1 && p == v) || q == 0))
									descriptions.push_back(FeatureDescriptor{ r, rt, k, p, q, v, s, 1 });
							}
						}
					}
				}
			}
		}
		featuresCount = (int)descriptions.size();
	}

	static string GetType()
	{
		return "ZernikeInvariantsExtractor";
	}

	/// <summary>Zwraca typ cechy</summary>
	/// <returns>Typ klasyfikatora</returns>
	string getType() const override
	{
		return GetType();
	}

	bool getRectangleWindowsRequirement() const override
	{
		return true;
	}

	/// <summary>Zwraca opis ekstraktora cech</summary>
	/// <returns>Opis ekstraktora cech</returns>
	string toString() const override
	{
		string text = getType() + "\r\n";
		text += "Pmax: " + to_string(p_max) + "\r\n";
		text += "Qmax: " + to_string(q_max) + "\r\n";
		text += "Rings: " + to_string(rings) + "\r\n";
		text += "RingsType: " + to_string(ringsType) + "\r\n";
		text += "Width: " + to_string(d) + "\r\n";

		return text;
	}

	void loadImageData(const string path) override
	{
		auto[height, width, img] = loadImage(path, saveMode);
		this->width = width;
		this->height = height;

		int maxDim = max(width, height);

		calculateII2(img);

#pragma omp critical (RecalculateSpeeder)
		{
			if ((int)Speeder.size() < maxDim)
				speederUpdate(width);
		}

		for (int i = 0; i < height; i++)
			delete[] img[i];
		delete[] img;
	}

	virtual void loadImageData(const double* const* img, int height, int width) override
	{
		this->width = width;
		this->height = height;
		int maxDim = max(width, height);

		calculateII2(img);

		#pragma omp critical (RecalculateSpeeder)
		{
			if ((int)Speeder.size() < maxDim)
				speederUpdate(maxDim);
		}
	}

	void initializeExtractor(Point* sizes, int sca) override
	{
		if (!initialized)
			ZernikeII::initializeExtractor();

		for (int s1 = 0; s1 < sca; s1++)
		{
			int Wx = sizes[s1].wx;

			if (M_p_w.count(Wx) == 0)
			{
				this->M_p_w[Wx] = new complex<double>[p_max + 1];
				for (int p = 0; p <= p_max; p++)
					this->M_p_w[Wx][p] = (2.0 / pow(Wx, 2)) / (pi2 * a[p]);
			}
		}
	}

	virtual int extractFromWindowSmall(double* features, const int* featuresID, int fLength, int Wx, int Wy, int kp = 0, int jp = 0)
	{
		complex<double>* M_p = M_p_w[Wx];

		//return extractFromWindow(Wx, Wy, xp, yp);
		double jc = jp + 1 + (Wx - 1) / 2.0;
		double kc = kp + 1 + (Wx - 1) / 2.0;
		int N = Wx;

		vector<vector<vector<vector<complex<double>>>>> oldMoments(rings,
			vector<vector<vector<complex<double>>>>(ringsType + 1,
				vector<vector<complex<double>>>(p_max + 1,
					vector<complex<double>>(q_max + 1, complex<double>(0)))));

		int jc2 = (int)round(jc - 0.5);
		int kc2 = (int)round(kc - 0.5);

		vector<int> x1I(rings), y1I(rings), x2I(rings), y2I(rings);
		vector<int> x1_t(rings), y1_t(rings), x2_t(rings), y2_t(rings);
		for (int r = 0; r < rings; r++)
		{
			int WInner = (int)round(N * sqrt((rings - (r + 1)) / (double)rings));
			if (WInner % 2 == 1)
				WInner++;
			x1I[r] = (int)(jc - 0.5 - WInner / 2);
			y1I[r] = (int)(kc - 0.5 - WInner / 2);
			x2I[r] = x1I[r] + WInner - 1;
			y2I[r] = y1I[r] + WInner - 1;

			int wc = (int)round(N*sqrt((rings - r) / (double)rings));
			if (wc % 2 == 1)
				wc++;
			x1_t[r] = (int)(jc - 0.5 - wc / 2);
			y1_t[r] = (int)(kc - 0.5 - wc / 2);

			x2_t[r] = x1_t[r] + wc - 1;
			y2_t[r] = y1_t[r] + wc - 1;
		}

		for (int f = 0; f < fLength; f++)
		{
			int id = featuresID[f];
			const int &p = descriptions[id].p;
			const int &q = descriptions[id].q;
			const int &v = descriptions[id].v;
			const int &qq = descriptions[id].s;
			const int &r = descriptions[id].r;
			const int &rt = descriptions[id].rt;
			const int &k = descriptions[id].k;
			const int &kind = descriptions[id].kind;

			double nwc = sqrt(2) / ((double)N);
			int x1Inner = 0, x2Inner = 0, y1Inner = 0, y2Inner = 0;
			if (r < rings - 1)
			{
				x1Inner = x1I[r];
				y1Inner = y1I[r];
				x2Inner = x2I[r];
				y2Inner = y2I[r];
			}

			complex<double> c1 = 0;
			if (oldMoments[r][rt][p][q] == c1)
			{
				for (int s = q; s <= p; s += 2)
				{
					double tmp_s = Beta[p][q][s] * powReal(nwc, s);
					complex<double> sum_t;
					int sqn = (int)((s - q) / 2.0);
					for (int t = 0; t <= sqn; t++)
					{
						complex<double> tmp_t = (double)bc[sqn][t];
						complex<double> sum_u;
						int sqnsodd = (int)((s + q) / 2.0);
						for (int u = 0; u <= sqnsodd; u++)
						{
							complex<double> delta(0, 0);
							//if (deltasSpeeder[rt][r][t][u] == delta)
							//{
								delta = deltaII(Integrals[t][u], x1_t[r], y1_t[r], (x2_t[r] - x1_t[r] + 1));

								if (r < rings - 1 && rt == 0)
									delta -= deltaII(Integrals[t][u], x1Inner, y1Inner, abs(x2Inner - x1Inner + 1));
								//deltasSpeeder[rt][r][t][u] = delta;
							//}
							complex<double> tmp_u = (double)bc[sqnsodd][u] * Speeder[jc2][kc2][sqn - t][sqnsodd - u]; // go back here
							sum_u += tmp_u * delta;
						}
						sum_t += tmp_t * sum_u;
					}
					c1 += tmp_s * sum_t;
				}
				c1 *= M_p[p];
				oldMoments[r][rt][p][q] = c1;
			}
			else
				c1 = oldMoments[r][rt][p][q];

			complex<double> c2 = 0;
			if (oldMoments[r][rt][v][qq] == c2)
			{
				for (int s = qq; s <= v; s += 2)
				{
					double tmp_s = Beta[v][qq][s] * powReal(nwc, s);
					complex<double> sum_t;
					int sqn = (int)((s - qq) / 2.0);
					for (int t = 0; t <= sqn; t++)
					{
						complex<double> tmp_t = (double)bc[sqn][t];
						complex<double> sum_u;
						int sqnsodd = (int)((s + qq) / 2.0);
						for (int u = 0; u <= sqnsodd; u++)
						{
							complex<double> delta(0, 0);
							//if (deltasSpeeder[rt][r][t][u] == delta)
							//{
								delta = deltaII(Integrals[t][u], x1_t[r], y1_t[r], (x2_t[r] - x1_t[r] + 1));
								if (r < rings - 1 && rt == 0)
									delta -= deltaII(Integrals[t][u], x1Inner, y1Inner, abs(x2Inner - x1Inner + 1));
							//	deltasSpeeder[rt][r][t][u] = delta;
							//}
							complex<double> tmp_u = (double)bc[sqnsodd][u] * Speeder[jc2][kc2][sqn - t][sqnsodd - u]; // go back here
							sum_u += tmp_u * delta;
						}
						sum_t += tmp_t * sum_u;
					}
					c2 += tmp_s * sum_t;
				}
				c2 *= M_p[v];
				oldMoments[r][rt][v][qq] = c2;
			}
			else
				c2 = oldMoments[r][rt][v][qq];

			complex<double> c = pow(c1, k)*conj(c2);
			if (kind == 0) features[id] = real(c);
			else features[id] = imag(c);
		}
		return featuresCount;
	}

	/// <param name = 'featuresID'>Numery cech</param>
	/// <param name = 'Wx'>Szerokosc okna</param>
	/// <param name = 'Wy'>Wysokosc okna</param>
	/// <param name = 'xp'>X-pozycja okna (lewy gorny rog)</param>
	/// <param name = 'yp'>Y-pozycja okna (lewy gorny rog)</param>
	virtual int extractFromWindow(double* features, const int* featuresID, int fLength, int Wx, int Wy, int kp = 0, int jp = 0) override
	{
		if (fLength < 30)
		{
			return extractFromWindowSmall(features, featuresID, fLength, Wx, Wy, kp, jp);
		}
		else
		{
			complex<double>* M_p = M_p_w[Wx];

			//return extractFromWindow(Wx, Wy, xp, yp);
			double jc = jp + 1 + (Wx - 1) / 2.0;
			double kc = kp + 1 + (Wx - 1) / 2.0;
			int N = Wx;

			vector<vector<vector<vector<complex<double>>>>> deltasSpeeder(ringsType + 1,
				vector<vector<vector<complex<double>>>>(rings,
					vector<vector<complex<double>>>((p_max + q_max + 2) / 2,
						vector<complex<double>>((p_max + q_max + 2) / 2))));

			vector<vector<vector<vector<complex<double>>>>> oldMoments(rings,
				vector<vector<vector<complex<double>>>>(ringsType + 1,
					vector<vector<complex<double>>>(p_max + 1,
						vector<complex<double>>(q_max + 1, complex<double>(0)))));

			int jc2 = (int)round(jc - 0.5);
			int kc2 = (int)round(kc - 0.5);

			vector<int> x1I(rings), y1I(rings), x2I(rings), y2I(rings);
			vector<int> x1_t(rings), y1_t(rings), x2_t(rings), y2_t(rings);
			for (int r = 0; r < rings; r++)
			{
				int WInner = (int)round(N * sqrt((rings - (r + 1)) / (double)rings));
				if (WInner % 2 == 1)
					WInner++;
				x1I[r] = (int)(jc - 0.5 - WInner / 2);
				y1I[r] = (int)(kc - 0.5 - WInner / 2);
				x2I[r] = x1I[r] + WInner - 1;
				y2I[r] = y1I[r] + WInner - 1;

				int wc = (int)round(N*sqrt((rings - r) / (double)rings));
				if (wc % 2 == 1)
					wc++;
				x1_t[r] = (int)(jc - 0.5 - wc / 2);
				y1_t[r] = (int)(kc - 0.5 - wc / 2);

				x2_t[r] = x1_t[r] + wc - 1;
				y2_t[r] = y1_t[r] + wc - 1;
			}

			for (int f = 0; f < fLength; f++)
			{
				int id = featuresID[f];
				const int &p = descriptions[id].p;
				const int &q = descriptions[id].q;
				const int &v = descriptions[id].v;
				const int &qq = descriptions[id].s;
				const int &r = descriptions[id].r;
				const int &rt = descriptions[id].rt;
				const int &k = descriptions[id].k;
				const int &kind = descriptions[id].kind;

				double nwc = sqrt(2) / ((double)N);
				int x1Inner = 0, x2Inner = 0, y1Inner = 0, y2Inner = 0;
				if (r < rings - 1)
				{
					x1Inner = x1I[r];
					y1Inner = y1I[r];
					x2Inner = x2I[r];
					y2Inner = y2I[r];
				}

				complex<double> c1 = 0;
				if (oldMoments[r][rt][p][q] == c1)
				{
					for (int s = q; s <= p; s += 2)
					{
						double tmp_s = Beta[p][q][s] * powReal(nwc, s);
						complex<double> sum_t;
						int sqn = (int)((s - q) / 2.0);
						for (int t = 0; t <= sqn; t++)
						{
							complex<double> tmp_t = (double)bc[sqn][t];
							complex<double> sum_u;
							int sqnsodd = (int)((s + q) / 2.0);
							for (int u = 0; u <= sqnsodd; u++)
							{
								complex<double> delta(0, 0);
								if (deltasSpeeder[rt][r][t][u] == delta)
								{
									delta = deltaII(Integrals[t][u], x1_t[r], y1_t[r], (x2_t[r] - x1_t[r] + 1));

									if (r < rings - 1 && rt == 0)
										delta -= deltaII(Integrals[t][u], x1Inner, y1Inner, abs(x2Inner - x1Inner + 1));
									deltasSpeeder[rt][r][t][u] = delta;
								}
								complex<double> tmp_u = (double)bc[sqnsodd][u] * Speeder[jc2][kc2][sqn - t][sqnsodd - u]; // go back here
								sum_u += tmp_u * deltasSpeeder[rt][r][t][u];
							}
							sum_t += tmp_t * sum_u;
						}
						c1 += tmp_s * sum_t;
					}
					c1 *= M_p[p];
					oldMoments[r][rt][p][q] = c1;
				}
				else
					c1 = oldMoments[r][rt][p][q];

				complex<double> c2 = 0;
				if (oldMoments[r][rt][v][qq] == c2)
				{
					for (int s = qq; s <= v; s += 2)
					{
						double tmp_s = Beta[v][qq][s] * powReal(nwc, s);
						complex<double> sum_t;
						int sqn = (int)((s - qq) / 2.0);
						for (int t = 0; t <= sqn; t++)
						{
							complex<double> tmp_t = (double)bc[sqn][t];
							complex<double> sum_u;
							int sqnsodd = (int)((s + qq) / 2.0);
							for (int u = 0; u <= sqnsodd; u++)
							{
								complex<double> delta(0, 0);
								if (deltasSpeeder[rt][r][t][u] == delta)
								{
									delta = deltaII(Integrals[t][u], x1_t[r], y1_t[r], (x2_t[r] - x1_t[r] + 1));
									if (r < rings - 1 && rt == 0)
										delta -= deltaII(Integrals[t][u], x1Inner, y1Inner, abs(x2Inner - x1Inner + 1));
									deltasSpeeder[rt][r][t][u] = delta;
								}
								complex<double> tmp_u = (double)bc[sqnsodd][u] * Speeder[jc2][kc2][sqn - t][sqnsodd - u]; // go back here
								sum_u += tmp_u * deltasSpeeder[rt][r][t][u];
							}
							sum_t += tmp_t * sum_u;
						}
						c2 += tmp_s * sum_t;
					}
					c2 *= M_p[v];
					oldMoments[r][rt][v][qq] = c2;
				}
				else
					c2 = oldMoments[r][rt][v][qq];

				complex<double> c = pow(c1, k)*conj(c2);
				if (kind == 0) features[id] = real(c);
				else features[id] = imag(c);
			}
			return featuresCount;
		}
	}

	/// <summary>Ekstrachuje cechy z podanego obrazu</summary>
	/// <param name = 'Wx'>Szerokosc okna</param>
	/// <param name = 'Wy'>Wysokosc okna</param>
	/// <param name = 'xp'>X-pozycja okna (lewy gorny rog)</param>
	/// <param name = 'yp'>Y-pozycja okna (lewy gorny rog)</param>
	/// <returns>Ekstrachowane cechy</returns>
	virtual tuple<int, const double*> extractFromWindow(int Wx, int Wy, int kp = 0, int jp = 0) override
	{
		double* features = new double[featuresCount];
		int id = -1;

		double npow2Local = (2.0 / pow(Wx, 2));
		double* M_p_Local = new double[p_max + 1];
		for (int p = 0; p <= p_max; p++)
			M_p_Local[p] = npow2Local / (pi2 * a[p]);

		vector<double> F;
		double jc = jp + 1 + (Wx - 1) / 2.0;
		double kc = kp + 1 + (Wx - 1) / 2.0;
		int N = Wx;

		int jc2 = (int)round(jc - 0.5);
		int kc2 = (int)round(kc - 0.5);

		vector<vector<vector<vector<complex<double>>>>> deltasSpeeder(rings,
			vector<vector<vector<complex<double>>>>(ringsType + 1,
				vector<vector<complex<double>>>((p_max + q_max + 2) / 2,
					vector<complex<double>>((p_max + q_max + 2) / 2))));

		vector<vector<vector<vector<complex<double>>>>> oldMoments(rings,
			vector<vector<vector<complex<double>>>>(ringsType + 1,
				vector<vector<complex<double>>>(p_max + 1,
					vector<complex<double>>(q_max + 1))));

		for (int r = 0; r < rings; r++)
		{
			int wc = (int)round(N*sqrt((rings - r) / (double)rings));
			if (wc % 2 == 1)
				wc++;
			int x1 = (int)(jc - 0.5 - wc / 2);
			int y1 = (int)(kc - 0.5 - wc / 2);

			int x2 = x1 + wc - 1;
			int y2 = y1 + wc - 1;

			double nwc = sqrt(2) / ((double)N);
			int x1Inner = 0, x2Inner = 0, y1Inner = 0, y2Inner = 0, WInner = 0;
			if (r < rings - 1)
			{
				WInner = (int)round(N * sqrt((rings - (r + 1)) / (double)rings));
				if (WInner % 2 == 1)
					WInner++;
				x1Inner = (int)(jc - 0.5 - WInner / 2);
				y1Inner = (int)(kc - 0.5 - WInner / 2);
				x2Inner = x1Inner + WInner - 1;
				y2Inner = y1Inner + WInner - 1;
			}

			int hMax = 0;
			if (ringsType == 1)
				hMax = (r == rings - 1) ? 0 : 1;

			for (int rt = 0; rt <= hMax; rt++)
			{
				for (int p = 0; p <= p_max; p++)
				{
					complex<double> M = M_p_Local[p];
					for (int q = p % 2; q <= p; q += 2)
					{
						complex<double> sum_s(0.0);
						for (int s = q; s <= p; s += 2)
						{
							double tmp_s = Beta[p][q][s] * powReal(nwc, s);
							complex<double> sum_t(0.0);
							int sqn = (int)((s - q) / 2.0);
							for (int t = 0; t <= sqn; t++)
							{
								complex<double> tmp_t = (double)bc[sqn][t];
								complex<double> sum_u(0.0);
								int sqnsodd = (int)((s + q) / 2.0);
								for (int u = 0; u <= sqnsodd; u++)
								{
									complex<double> delta(0, 0);
									if (deltasSpeeder[r][rt][t][u] == delta)
									{
										delta = deltaII(Integrals[t][u], x1, y1, (x2 - x1 + 1));
										if (r < rings - 1 && rt == 0)
											delta -= deltaII(Integrals[t][u], x1Inner, y1Inner, abs(x2Inner - x1Inner + 1));
										deltasSpeeder[r][rt][t][u] = delta;
									}
									complex<double> tmp_u = (double)bc[sqnsodd][u] * Speeder[jc2][kc2][sqn - t][sqnsodd - u]; // go back here
									sum_u += tmp_u * deltasSpeeder[r][rt][t][u];
								}
								sum_t += tmp_t * sum_u;
							}
							sum_s += tmp_s * sum_t;
						}
						oldMoments[r][rt][p][q] = M * sum_s;
						//cout << "r: " << r << ", rt: " << rt << ", p: " << p << ", q: " << q << ", F: " << setw(16) << setprecision(10) << abs(oldMoments[r][rt][p][q]) << endl;
					}
				}
			}
		}

		for (int i = 0; i < descriptions.size(); i++)
		{
			int r = descriptions[i].r;
			int rt = descriptions[i].rt;
			int k = descriptions[i].k;
			int p = descriptions[i].p;
			int q = descriptions[i].q;
			int v = descriptions[i].v;
			int s = descriptions[i].s;
			int kind = descriptions[i].kind;

			complex<double> c1 = oldMoments[r][rt][p][q];
			complex<double> c2 = oldMoments[r][rt][v][s];
			complex<double> c = pow(c1, k)*conj(c2);
			if (kind == 0) features[i] = real(c);
			else features[i] = imag(c);
		}
		delete[] M_p_Local;

		return make_tuple(featuresCount, features);
	}

	/// <summary>Ekstrachuje cechy z podanych plikow</summary>
	/// <param name = 'paths'>Wektor sciezek do plikow</param>
	virtual tuple<int, int, const double* const*> extractMultipleFeatures(const vector<string> &paths)  override
	{
		const double ** X = new const double*[paths.size()];

#pragma omp parallel for num_threads(OMP_NUM_THR)
		for (int i = 0; i < (int)paths.size(); i++)
		{
			ZernikeIIinvariants zernike(this);

			auto[fc, fets] = zernike.extractFeatures(paths[i]);
			X[i] = fets;
		}
		return make_tuple((int)paths.size(), featuresCount, X);
	}
};

class ZernikeFPII : public Extractor
{
protected:
	//vector<vector<vector<vector<vector<vector<complex<double>>>>>>> Integrals;
	complex<double> ******Integrals = nullptr;

	static vector<vector<vector<vector<complex<double>>>>> SpeederPartial;
	static vector<vector<unsigned long long int>> bc;
	static vector<vector<vector<long long>>> Beta;
	static vector<double> a;

	static bool initialized;

	static void initializeBetas()
	{
		string fileName = "betas.bin";

		ifstream file(fileName, std::ios_base::binary);
		if (file.good())
		{
			int pSize, qSize;
			file.read(reinterpret_cast<char*>(&pSize), sizeof(pSize));
			file.read(reinterpret_cast<char*>(&qSize), sizeof(qSize));
			if (pSize != pLimit || qSize != qLimit)
			{
				file.close();
				remove(fileName.c_str());
			}
		}

		if (file.is_open() && file.good())
		{
			for (int n = 0; n <= pLimit; n++)
			{
				Beta[n].resize(n + 1);
				for (int m = n % 2; m <= n; m += 2)
				{
					Beta[n][m].resize(n + 1);
					file.read(reinterpret_cast<char*>(Beta[n][m].data()), sizeof(long long) * (n + 1));
				}
			}
			file.read(reinterpret_cast<char*>(a.data()), sizeof(double) * (pLimit + 1));
		}
		else
		{
			for (int n = 0; n <= pLimit; n++)
			{
				Beta[n].resize(n + 1);
				for (int m = n % 2; m <= n; m += 2)
				{
					Beta[n][m].resize(n + 1);
					for (int s = 0; s <= (n - m) / 2; s++)
					{
						int k = n - 2 * s;
						Beta[n][m][k] = simplificationQ(n, m, s);
					}
				}
				a[n] = (1.0 / (2 * (n + 1)));
			}

			ofstream filetxt("betas_poly2.txt");
			for (int n = 0; n <= pLimit; n++)
			{
				for (int m = n % 2; m <= n; m += 2)
				{
					filetxt << "p: " << setw(5) << n << ", " << "q: " << setw(5) << m << " --- ";
					for (int s = 0; s < Beta[n][m].size(); s++)
					{
						filetxt << setw(10) << Beta[n][m][s] << "r ^ " << s << "   ";
					}
					filetxt << endl;
				}
			}
			filetxt.close();

			ofstream file(fileName, std::ios_base::binary);

			file.write(reinterpret_cast<const char*>(&pLimit), sizeof(pLimit));
			file.write(reinterpret_cast<const char*>(&qLimit), sizeof(qLimit));
			for (int n = 0; n <= pLimit; n++)
				for (int m = n % 2; m <= n; m += 2)
					file.write(reinterpret_cast<const char*>(Beta[n][m].data()), sizeof(long long) * (n + 1));
			file.write(reinterpret_cast<const char*>(a.data()), sizeof(double) * (pLimit + 1));
			file.close();
		}
		if (file.is_open())
			file.close();
	}

	static int simplificationQ(int n, int m, int k)
	{
		if (k == 0 && n == 0 && m == 0)
			return 1;

		vector<int> licznik(n - k - 1);
		if (n - k == 0 || n - k == 1)
		{
			licznik.resize(1);
			licznik[0] = 1;
		}
		else
			iota(licznik.begin(), licznik.end(), 2);

		vector<int> mianownik;
		mianownik.push_back(1);
		for (int i = 0; i < k - 1; i++)
			mianownik.push_back(i + 2);
		for (int i = 0; i < (n + m) / 2.0 - k - 1; i++)
			mianownik.push_back(i + 2);
		for (int i = 0; i < (n - m) / 2.0 - k - 1; i++)
			mianownik.push_back(i + 2);

		vector<int> ind_licznik(licznik.size());
		fill(ind_licznik.begin(), ind_licznik.end(), 1);

		vector<int> ind_mianownik(mianownik.size());
		fill(ind_mianownik.begin(), ind_mianownik.end(), 1);

		while (true)
		{
			for (int i = 0; i < (int)mianownik.size(); i++)
			{
				for (int j = 0; j < (int)licznik.size(); j++)
				{
					int a = gcd(licznik[j], mianownik[i]);
					if (a > 1)
					{
						licznik[j] /= a;
						mianownik[i] /= a;
						break;
					}
				}
			}
			vector<int> res_licznik;
			vector<int> res_mianownik;
			for (int i = 0; i < (int)mianownik.size(); i++)
				if (mianownik[i] != 1)
					res_mianownik.push_back(mianownik[i]);
			for (int i = 0; i < (int)licznik.size(); i++)
				if (licznik[i] != 1)
					res_licznik.push_back(licznik[i]);
			if (licznik.size() == res_licznik.size() && mianownik.size() == res_mianownik.size())
				break;
			mianownik = res_mianownik;
			licznik = res_licznik;
		}

		licznik.push_back(1);
		mianownik.push_back(1);
		return (int)(pow(-1, k)*prod(licznik) / prod(mianownik));
	}

	static void initialzeBinomials()
	{
		string fileName = "binomialsZernike.bin";
		int n = pLimit + 2 * qLimit + 1;

		ifstream file(fileName, std::ios_base::binary);
		if (file.good())
		{
			int qSize, pSize;
			file.read(reinterpret_cast<char*>(&qSize), sizeof(qSize));
			file.read(reinterpret_cast<char*>(&pSize), sizeof(pSize));
			if (pSize != pLimit || qSize != qLimit)
			{
				file.close();
				remove(fileName.c_str());
			}
		}

		if (file.is_open() && file.good())
		{
			bc.resize(n);
			for (int i = 0; i < n; i++)
			{
				bc[i].resize(i + 1);
				file.read(reinterpret_cast<char*>(bc[i].data()), sizeof(unsigned long long) * (i + 1));
			}
		}
		else
		{
			binomial(pLimit, qLimit, bc);

			ofstream file(fileName, std::ios_base::binary);
			file.write(reinterpret_cast<const char*>(&qLimit), sizeof(qLimit));
			file.write(reinterpret_cast<const char*>(&pLimit), sizeof(pLimit));
			for (int i = 0; i < n; i++)
				file.write(reinterpret_cast<const char*>(bc[i].data()), sizeof(unsigned long long) * (i + 1));
			file.close();
		}
		if (file.is_open())
			file.close();
	}

	static void initialzeSpeeder()
	{
		string fileName = "speedersZernikePartial.bin";

		int maxExponent = (int)ceil(pLimit / 2.0 + qLimit / 2.0);

		ifstream file(fileName, std::ios_base::binary);
		if (file.good())
		{
			int maxSize, minSize, qSize, pSize;
			file.read(reinterpret_cast<char*>(&maxSize), sizeof(maxSize));
			file.read(reinterpret_cast<char*>(&minSize), sizeof(minSize));
			file.read(reinterpret_cast<char*>(&qSize), sizeof(qSize));
			file.read(reinterpret_cast<char*>(&pSize), sizeof(pSize));
			if (pSize != pLimit || qSize != qLimit || maxSize != predictedMaxSize || minSize != predictedMinSize)
			{
				file.close();
				remove(fileName.c_str());
			}
		}

		if (file.is_open() && file.good())
		{
			SpeederPartial.resize(predictedMaxSize - predictedMinSize);
			for (int i = predictedMinSize; i < predictedMaxSize; i++)
			{
				SpeederPartial[i - predictedMinSize].resize(predictedMaxSize - predictedMinSize);
				for (int j = predictedMinSize; j < predictedMaxSize; j++)
				{
					SpeederPartial[i - predictedMinSize][j - predictedMinSize].resize(maxExponent + 1);
					for (int expT = 0; expT <= maxExponent; expT++)
					{
						SpeederPartial[i - predictedMinSize][j - predictedMinSize][expT].resize(maxExponent + 2);
						file.read(reinterpret_cast<char*>(SpeederPartial[i - predictedMinSize][j - predictedMinSize][expT].data()), sizeof(complex<double>) * (maxExponent + 2));
					}
				}
			}
		}
		else
		{
			speeder(predictedMaxSize, predictedMinSize);

			ofstream file(fileName, std::ios_base::binary);
			file.write(reinterpret_cast<const char*>(&predictedMaxSize), sizeof(predictedMaxSize));
			file.write(reinterpret_cast<const char*>(&predictedMinSize), sizeof(predictedMinSize));
			file.write(reinterpret_cast<const char*>(&qLimit), sizeof(qLimit));
			file.write(reinterpret_cast<const char*>(&pLimit), sizeof(pLimit));
			for (int i = 0; i < predictedMaxSize - predictedMinSize; i++)
				for (int j = 0; j < predictedMaxSize - predictedMinSize; j++)
					for (int expT = 0; expT <= maxExponent; expT++)
						file.write(reinterpret_cast<const char*>(SpeederPartial[i][j][expT].data()), sizeof(complex<double>) * (maxExponent + 2));
			file.close();
		}
		if (file.is_open())
			file.close();
	}

	static void speeder(int maxsize, int minsize)
	{
		SpeederPartial.clear();
		SpeederPartial.resize(maxsize - minsize);
		int maxExponent = (int)ceil(pLimit / 2.0 + qLimit / 2.0);
		for (int j = minsize; j < maxsize; j++)
		{
			double jc = j + 0.5;
			SpeederPartial[j - minsize] = vector<vector<vector<complex<double>>>>(maxsize - minsize);
			for (int k = minsize; k < maxsize; k++)
			{
				double kc = k + 0.5;
				SpeederPartial[j - minsize][k - minsize] = vector<vector<complex<double>>>(maxExponent + 1);
				for (int expT = 0; expT <= maxExponent; expT++)
				{
					SpeederPartial[j - minsize][k - minsize][expT] = vector<complex<double>>(maxExponent + 2);
					complex<double> factorT = pow(complex<double>(-kc, jc), expT);
					for (int expU = 0; expU <= maxExponent + 1; expU++)
					{
						complex<double> factorU = pow(complex<double>(-kc, -jc), expU);
						SpeederPartial[j - minsize][k - minsize][expT][expU] = factorT * factorU;
					}
				}
			}
		}
	}
	
	void calculateII2(const double* const* img, int d)
	{
		clearImageData();

		int ileS1 = (int)ceil(1.0*width / d);
		int ileS2 = (int)ceil(1.0*height / d);
		int s1 = width;
		int s2 = height;
		int tlim = p_max / 2;
		int tg = (int)ceil((p_max - min(q_max, p_max)) / 2.0);

		Integrals = new complex<double>*****[ileS2];
		for (int iS2 = 0; iS2 < ileS2; iS2++)
		{
			int yS2p = iS2 * d;
			int yS2k = (iS2 + 1)*d - 1;
			if (yS2k >= height)
				yS2k = height - 1;
			Integrals[iS2] = new complex<double>****[ileS1];
			for (int iS1 = 0; iS1 < ileS1; iS1++)
			{
				int xS1p = iS1 * d;
				int xS1k = (iS1 + 1)*d + -1;
				if (xS1k >= width)
					xS1k = width - 1;
				int maxu = p_max - tg;
				Integrals[iS2][iS1] = new complex<double>***[tlim + 1];

				for (int t = 0; t <= tlim; t++)
				{
					if (t > tg)
						maxu = p_max - t;
					Integrals[iS2][iS1][t] = new complex<double>**[maxu + 1];
#pragma omp parallel for num_threads(OMP_NUM_THR)
					for (int u = t; u <= maxu; u++)
					{
						complex<double>* ll = new complex<double>[xS1k - xS1p + 1];
						Integrals[iS2][iS1][t][u] = new complex<double>*[yS2k - yS2p + 1];
						for (int y = yS2p; y <= yS2k; y++)
						{
							Integrals[iS2][iS1][t][u][y - yS2p] = new complex<double>[xS1k - xS1p + 1];
							for (int x = xS1p; x <= xS1k; x++)
							{
								int xx = x - xS1p;
								int yy = y - yS2p;
								complex<double> a = img[y][x] * 
									// (powComQuick(complex<double>(xx + 1, -(yy + 1)), t)
									//*powComQuick(complex<double>(xx + 1, yy + 1), u));
								(powIntQuick(powInt(xx + 1, 2) + powInt(yy + 1, 2), t)
									* powComQuick(complex<double>(xx + 1, yy + 1), u - t));
								complex<double> s;
								if (xx > 0)
									s = ll[xx - 1] + a;
								else
									s = a;
								ll[xx] = s;
								if (yy > 0)
									s = s + Integrals[iS2][iS1][t][u][yy - 1][xx];
								Integrals[iS2][iS1][t][u][yy][xx] = s;
							}
						}
						delete[] ll;
					}
				}
			}
		}
		for (int iS2 = 0; iS2 < ileS2; iS2++)
		{
			int yS2p = iS2 * d;
			int yS2k = (iS2 + 1)*d - 1;
			if (yS2k >= height)
				yS2k = height - 1;
			for (int iS1 = 0; iS1 < ileS1; iS1++)
			{
				int xS1p = iS1 * d;
				int xS1k = (iS1 + 1)*d + -1;
				if (xS1k >= width)
					xS1k = width - 1;
				int maxu = p_max - tg;
				for (int t = 0; t <= tlim; t++)
				{
					if (t > tg)
						maxu = p_max - t;
#pragma omp parallel for num_threads(OMP_NUM_THR)
					for (int u = 0; u < t; u++)
					{
						Integrals[iS2][iS1][t][u] = new complex<double>*[yS2k - yS2p + 1];
						for (int y = yS2p; y <= yS2k; y++)
						{
							Integrals[iS2][iS1][t][u][y - yS2p] = new complex<double>[xS1k - xS1p + 1];
							for (int x = xS1p; x <= xS1k; x++)
							{
								int xx = x - xS1p;
								int yy = y - yS2p;
								Integrals[iS2][iS1][t][u][yy][xx] = conj(Integrals[iS2][iS1][u][t][yy][xx]);
							}
						}
					}
				}
			}

		}
	}

	complex<double> deltaII(const complex<double>*const*Integral, const int jp, const int kp, const int Nj, const int Nk) const
	{
		complex<double> delta;

		if (jp == 0 && kp == 0)
			delta = Integral[jp + Nj - 1][kp + Nk - 1];
		else if (jp == 0)
			delta = Integral[jp + Nj - 1][kp + Nk - 1] - Integral[jp + Nj - 1][kp - 1];
		else if (kp == 0)
			delta = Integral[jp + Nj - 1][kp + Nk - 1] - Integral[jp - 1][kp + Nk - 1];
		else
			delta = Integral[jp + Nj - 1][kp + Nk - 1] - Integral[jp + Nj - 1][kp - 1]
			- Integral[jp - 1][kp + Nk - 1] + Integral[jp - 1][kp - 1];
		return delta;
	}

	complex<double> deltaP(const complex<double>*const*const*const*const*const*Integrals, const int d, const int yp, const int xp, const int N, const int t, const int u) const
	{
		complex<double> deltaP = 0;
		int yk = yp + N - 1;
		int xk = xp + N - 1;
		int V[2][4] = { { yp, yp, yk, yk },{ xp, xk, xk, xp } };
		for (int i = 0; i < 4; i++)
		{
			V[0][i] = (int)floor(1.0*V[0][i] / d);
			V[1][i] = (int)floor(1.0*V[1][i] / d);
		}
		if (V[0][0] == V[0][1] && V[1][0] == V[1][1] && V[0][0] == V[0][2] && V[1][0] == V[1][2] && V[0][0] == V[0][3] && V[1][0] == V[1][3])
		{
			//wszystkie w jednym calkowym
			//cout << "Wszystkie w jednym" << endl;
			int yy1 = yp - V[0][0] * d;
			int xx1 = xp - V[1][0] * d;
			deltaP = deltaII(Integrals[V[0][0]][V[1][0]][t][u], yy1, xx1, N, N);
		}
		else if (V[0][0] == V[0][3] && V[1][0] == V[1][3] && V[0][2] == V[0][1] && V[1][2] == V[1][1])
		{
			// obok siebie
			//cout << "Obok siebie" << endl;
			int yy1 = yp - V[0][0] * d;
			int xx1 = xp - V[1][0] * d;
			int xx2 = 0;
			deltaP = deltaII(Integrals[V[0][0]][V[1][0]][t][u], yy1, xx1, N, V[1][1] * d - xp) +
				deltaII(Integrals[V[0][1]][V[1][1]][t][u], yy1, xx2, N, xk - V[1][1] * d + 1);
		}
		else if (V[0][0] == V[0][1] && V[1][0] == V[1][1] && V[0][2] == V[0][3] && V[1][2] == V[1][3])
		{
			// jedno nad drugim
			//cout << "Jedno pod drugim" << endl;
			int yy1 = yp - V[0][0] * d;
			int xx1 = xp - V[1][0] * d;
			int yy2 = 0;
			deltaP = deltaII(Integrals[V[0][0]][V[1][0]][t][u], yy1, xx1, V[0][2] * d - yp, N) +
				deltaII(Integrals[V[0][2]][V[1][2]][t][u], yy2, xx1, yk - V[0][2] * d + 1, N);
		}
		else
		{
			//cout << "W czterech" << endl;
			int yy1 = yp - V[0][0] * d;
			int xx1 = xp - V[1][0] * d;
			int Ny1 = V[0][2] * d - yp;
			int Nx1 = V[1][1] * d - xp;
			int Ny2 = yk - V[0][2] * d + 1;
			int Nx2 = xk - V[1][1] * d + 1;
			deltaP = deltaII(Integrals[V[0][0]][V[1][0]][t][u], yy1, xx1, Ny1, Nx1) +
				deltaII(Integrals[V[0][1]][V[1][1]][t][u], yy1, 0, Ny1, Nx2) +
				deltaII(Integrals[V[0][2]][V[1][2]][t][u], 0, 0, Ny2, Nx2) +
				deltaII(Integrals[V[0][3]][V[1][3]][t][u], 0, xx1, Ny2, Nx1);
		}
		return deltaP;
	}

	vector<complex<double>> deltaPart(const complex<double>*const*const*const*const*const*Integrals, const int d, const int yp, const int xp, const int N, const int t, const int u) const
	{
		vector<complex<double>> delty;
		complex<double> deltaP = 0;
		int yk = yp + N - 1;
		int xk = xp + N - 1;
		int V[2][4] = { { yp, yp, yk, yk },{ xp, xk, xk, xp } };
		for (int i = 0; i < 4; i++)
		{
			V[0][i] = (int)floor(1.0*V[0][i] / d);
			V[1][i] = (int)floor(1.0*V[1][i] / d);
		}
		if (V[0][0] == V[0][1] && V[1][0] == V[1][1] && V[0][0] == V[0][2] && V[1][0] == V[1][2] && V[0][0] == V[0][3] && V[1][0] == V[1][3])
		{
			//wszystkie w jednym calkowym
			//cout << "Wszystkie w jednym" << endl;
			int yy1 = yp - V[0][0] * d;
			int xx1 = xp - V[1][0] * d;
			delty.push_back(deltaII(Integrals[V[0][0]][V[1][0]][t][u], yy1, xx1, N, N));
		}
		else if (V[0][0] == V[0][3] && V[1][0] == V[1][3] && V[0][2] == V[0][1] && V[1][2] == V[1][1])
		{
			// obok siebie
			//cout << "Obok siebie" << endl;
			int yy1 = yp - V[0][0] * d;
			int xx1 = xp - V[1][0] * d;
			int xx2 = 0;
			delty.push_back(deltaII(Integrals[V[0][0]][V[1][0]][t][u], yy1, xx1, N, V[1][1] * d - xp));
			delty.push_back(deltaII(Integrals[V[0][1]][V[1][1]][t][u], yy1, xx2, N, xk - V[1][1] * d + 1));
		}
		else if (V[0][0] == V[0][1] && V[1][0] == V[1][1] && V[0][2] == V[0][3] && V[1][2] == V[1][3])
		{
			// jedno nad drugim
			//cout << "Jedno pod drugim" << endl;
			int yy1 = yp - V[0][0] * d;
			int xx1 = xp - V[1][0] * d;
			int yy2 = 0;
			delty.push_back(deltaII(Integrals[V[0][0]][V[1][0]][t][u], yy1, xx1, V[0][2] * d - yp, N));
			delty.push_back(deltaII(Integrals[V[0][2]][V[1][2]][t][u], yy2, xx1, yk - V[0][2] * d + 1, N));
		}
		else
		{
			//cout << "W czterech" << endl;
			int yy1 = yp - V[0][0] * d;
			int xx1 = xp - V[1][0] * d;
			int Ny1 = V[0][2] * d - yp;
			int Nx1 = V[1][1] * d - xp;
			int Ny2 = yk - V[0][2] * d + 1;
			int Nx2 = xk - V[1][1] * d + 1;
			delty.push_back(deltaII(Integrals[V[0][0]][V[1][0]][t][u], yy1, xx1, Ny1, Nx1));
			delty.push_back(deltaII(Integrals[V[0][3]][V[1][3]][t][u], 0, xx1, Ny2, Nx1)); //jedno pod drugim
			delty.push_back(deltaII(Integrals[V[0][1]][V[1][1]][t][u], yy1, 0, Ny1, Nx2)); //jedno obok drugiego
			delty.push_back(deltaII(Integrals[V[0][2]][V[1][2]][t][u], 0, 0, Ny2, Nx2));
		}
		return delty;
	}

	struct FeatureDescriptor
	{
		int r;
		int rt;
		int p;
		int q;
	};

	vector<FeatureDescriptor> descriptions;

	const int p_max;
	const int q_max;
	const int rings;
	const int ringsType;
	const int d;

	static constexpr double pi2 = 2 * M_PI;
	map<int, complex<double>*> M_p_w;

	ZernikeFPII(ZernikeFPII * parent) : p_max{ parent->p_max }, q_max{ parent->q_max }, rings{ parent->rings }, ringsType{ parent->ringsType }, d{ parent->d }
	{
		this->saveMode = parent->saveMode;

		for (map<int, complex<double>*>::iterator iter = M_p_w.begin(); iter != M_p_w.end(); ++iter)
		{
			int k = iter->first;

			this->M_p_w[k] = new complex<double>[p_max + 1];
			for (int p = 0; p <= p_max; p++)
				this->M_p_w[k][p] = parent->M_p_w[k][p];
		}

		this->descriptions = parent->descriptions;
		this->featuresCount = parent->featuresCount;
	}
public:
	using Extractor::extractFromWindow;

	static void initializeExtractor()
	{
		if (!initialized)
		{
			Beta.resize(ZernikeII::pLimit + 1);
			a.resize(ZernikeII::pLimit + 1);

			initializeBetas();
			initialzeBinomials();
			initialzeSpeeder();

			initialized = true;
		}
	}

	static void clearMemory()
	{
		SpeederPartial = vector<vector<vector<vector<complex<double>>>>>();
		bc = vector<vector<unsigned long long int>>();
		Beta = vector<vector<vector<long long>>>();
		a = vector<double>();

		initialized = false;
	}

	~ZernikeFPII()
	{
		clearImageData();

		for (map<int, complex<double>*>::iterator iter = M_p_w.begin(); iter != M_p_w.end(); ++iter)
			delete[] M_p_w[iter->first];
		M_p_w.clear();

	}

	ZernikeFPII() : ZernikeFPII(8, 8, 6, 0, 100, SaveFileType::text) {}

	ZernikeFPII(const int p_max, const int q_max, const int rings, const int ringsType, const int d, SaveFileType fileType = SaveFileType::text) 
		: p_max{ p_max }, q_max{ q_max }, rings{ rings }, ringsType{ ringsType }, d{ d }
	{
		if (!initialized)
			initializeExtractor();
		this->saveMode = fileType;

		for (int r = 0; r < rings; r++)
		{
			int hMax = 0;
			if (ringsType == 1)
				hMax = (r == rings - 1) ? 0 : 1;

			for (int rt = 0; rt <= hMax; rt++)
			{
				for (int p = 0; p <= p_max; p++)
				{
					for (int q = p % 2; q <= min(p, q_max); q += 2)
						//for (int q = p % 2; q <= p; q += 2)
					{
						descriptions.push_back(FeatureDescriptor{ r, rt, p, q });
					}
				}
			}
		}
		featuresCount = (int)descriptions.size();
	}

	static string GetType()
	{
		return "ZernikeFPiiExtractor";
	}

	/// <summary>Zwraca typ cechy</summary>
	/// <returns>Typ klasyfikatora</returns>
	virtual string getType() const override
	{
		return GetType();
	}

	bool getRectangleWindowsRequirement() const override
	{
		return true;
	}

	/// <summary>Zwraca opis ekstraktora cech</summary>
	/// <returns>Opis ekstraktora cech</returns>
	virtual string toString() const override
	{
		string text = getType() + "\r\n";
		text += "Pmax: " + to_string(p_max) + "\r\n";
		text += "Qmax: " + to_string(q_max) + "\r\n";
		text += "Rings: " + to_string(rings) + "\r\n";
		text += "RingsType: " + to_string(ringsType) + "\r\n";
		text += "Width: " + to_string(d) + "\r\n";

		return text;
	}

	virtual void loadImageData(const string path) override
	{
		clearImageData();
		auto[height, width, img] = loadImage(path, saveMode);
		this->width = width;
		this->height = height;

		calculateII2(img, d);

		for (int i = 0; i < height; i++)
			delete[] img[i];
		delete[] img;
	}

	virtual void loadImageData(const double* const* img, int height, int width) override
	{
		clearImageData();
		this->width = width;
		this->height = height;

		calculateII2(img, d);
	}

	void clearImageData() override
	{
		if (Integrals != nullptr)
		{
			int ileS1 = (int)ceil(1.0*width / d);
			int ileS2 = (int)ceil(1.0*height / d);
			int s1 = width;
			int s2 = height;
			int tlim = p_max / 2;
			int tg = (int)ceil((p_max - min(q_max, p_max)) / 2.0);

			for (int iS2 = 0; iS2 < ileS2; iS2++)
			{
				int yS2p = iS2 * d;
				int yS2k = (iS2 + 1)*d - 1;
				if (yS2k >= height)
					yS2k = height - 1;

				for (int iS1 = 0; iS1 < ileS1; iS1++)
				{
					int maxu = p_max - tg;

					for (int t = 0; t <= tlim; t++)
					{
						if (t > tg)
							maxu = p_max - t;
						for (int u = 0; u <= maxu; u++)
						{
							for (int y = yS2p; y <= yS2k; y++)
							{
								delete[] Integrals[iS2][iS1][t][u][y - yS2p];
							}
							delete[] Integrals[iS2][iS1][t][u];
						}
						delete[] Integrals[iS2][iS1][t];
					}
					delete[] Integrals[iS2][iS1];
				}
				delete[] Integrals[iS2];
			}
			delete[] Integrals;
		}
		Integrals = nullptr;
	}

	void initializeExtractor(Point* sizes, int sca) override
	{
		if (!initialized)
			initializeExtractor();

		for (int s1 = 0; s1 < sca; s1++)
		{
			int Wx = sizes[s1].wx;

			if (M_p_w.count(Wx) == 0)
			{
				this->M_p_w[Wx] = new complex<double>[p_max + 1];
				for (int p = 0; p <= p_max; p++)
					this->M_p_w[Wx][p] = (2.0 / pow(Wx, 2)) / (pi2 * a[p]);
			}
		}
	}

	virtual int extractFromWindowSmall(double* features, const int* featuresID, int fLength, int Wx, int Wy, int kp = 0, int jp = 0)
	{
		complex<double>* M_p = M_p_w[Wx];

		//return extractFromWindow(Wx, Wy, xp, yp);
		double jc = jp + 1 + (Wx - 1) / 2.0;
		double kc = kp + 1 + (Wx - 1) / 2.0;
		int N = Wx;

		vector<int> x1I(rings), y1I(rings), x2I(rings), y2I(rings);
		vector<int> x1_t(rings), y1_t(rings), x2_t(rings), y2_t(rings);
		vector<int> wc_t(rings);
		for (int r = 0; r < rings; r++)
		{
			int WInner = (int)round(N * sqrt((rings - (r + 1)) / (double)rings));
			if (WInner % 2 == 1)
				WInner++;
			x1I[r] = (int)(jc - 0.5 - WInner / 2);
			y1I[r] = (int)(kc - 0.5 - WInner / 2);
			x2I[r] = x1I[r] + WInner - 1;
			y2I[r] = y1I[r] + WInner - 1;

			int wc = (int)round(N*sqrt((rings - r) / (double)rings));
			if (wc % 2 == 1)
				wc++;
			wc_t[r] = wc;
			x1_t[r] = (int)(jc - 0.5 - wc / 2);
			y1_t[r] = (int)(kc - 0.5 - wc / 2);

			x2_t[r] = x1_t[r] + wc - 1;
			y2_t[r] = y1_t[r] + wc - 1;
		}

		for (int f = 0; f < fLength; f++)
		{
			int id = featuresID[f];
			const int &p = descriptions[id].p;
			const int &q = descriptions[id].q;
			const int &r = descriptions[id].r;
			const int &rt = descriptions[id].rt;

			double nwc = sqrt(2) / ((double)N);
			int x1Inner = 0, x2Inner = 0, y1Inner = 0, y2Inner = 0;
			if (r < rings - 1)
			{
				x1Inner = x1I[r];
				y1Inner = y1I[r];
				x2Inner = x2I[r];
				y2Inner = y2I[r];
			}

			vector<int> jc2;
			vector<int> kc2;
			int jo1 = (int)(x1_t[r] - floor(x1_t[r] / d)*d);
			int ko1 = (int)(y1_t[r] - floor(y1_t[r] / d)*d);
			jc2.push_back((int)((1.0 / 2 * (2 * jo1 + wc_t[r] - 1) - 0.5) + 1));
			kc2.push_back((int)((1.0 / 2 * (2 * ko1 + wc_t[r] - 1) - 0.5) + 1));
			if (jo1 + wc_t[r] > d)
			{
				jc2.push_back(jc2[0] - d);
				kc2.push_back(kc2[0]);
			}
			if (ko1 + wc_t[r] > d)
			{
				kc2.push_back(kc2[0] - d);
				jc2.push_back(jc2[0]);
			}
			if (jc2.size() == 3)
			{
				kc2.push_back(kc2[0] - d);
				jc2.push_back(jc2[0] - d);
			}

			complex<double> M = 0;
			for (int s = q; s <= p; s += 2)
			{
				double tmp_s = Beta[p][q][s] * powReal(nwc, s);
				complex<double> sum_t;
				int sqn = (int)((s - q) / 2.0);
				for (int t = 0; t <= sqn; t++)
				{
					complex<double> tmp_t = (double)bc[sqn][t];
					complex<double> sum_u;
					int sqnsodd = (int)((s + q) / 2.0);
					for (int u = 0; u <= sqnsodd; u++)
					{
						complex<double> sum(0, 0);
						complex<double> Lc(0.0);
						//for (int part = 0; part < jc2.size(); part++)
						//{
						//	sum += deltasSpeeder[part][r][rt][t][u];
						//}
						//if (sum == complex<double>(0.0))
						//{
							vector<complex<double>> party = deltaPart(Integrals, d, x1_t[r], y1_t[r], (x2_t[r] - x1_t[r] + 1), t, u);
							if (r < rings - 1 && rt == 0)
							{
								vector<complex<double>> party2 = deltaPart(Integrals, d, x1Inner, y1Inner, abs(x2Inner - x1Inner + 1), t, u);
								if (party.size() == 1)
								{
									party[0] -= party2[0];
								}
								else if (party.size() == 2)
								{
									if (party2.size() == 2)
									{
										for (int part = 0; part < jc2.size(); part++)
											party[part] -= party2[part];
									}
									else
									{
										int id_x1 = (int)floor(jp / d);
										int id_x1i = (int)floor(x1Inner / d);
										int id_y1 = (int)floor(kp / d);
										int id_y1i = (int)floor(y1Inner / d);
										if (id_x1 == id_x1i && id_y1 == id_y1i)
										{
											party[0] -= party2[0];
										}
										else
											party[1] -= party2[0];
									}

								}
								else
								{
									if (party.size() == party2.size())
									{
										for (int part = 0; part < jc2.size(); part++)
											party[part] -= party2[part];
									}
									else
									{
										int id_x1 = (int)floor(jp / d);
										int id_x1i = (int)floor(x1Inner / d);
										int id_y1 = (int)floor(kp / d);
										int id_y1i = (int)floor(y1Inner / d);
										if (party2.size() == 1)
										{
											if (id_x1 == id_x1i && id_y1 == id_y1i)
											{
												party[0] -= party2[0];
											}
											else if (id_x1 == id_x1i && id_y1 != id_y1i)
											{
												party[2] -= party2[0];
											}
											else if (id_x1 != id_x1i && id_y1 == id_y1i)
											{
												party[1] -= party2[0];
											}
											else
											{
												party[3] -= party2[0];
											}
										}
										else if (party2.size() == 2)
										{
											if (id_x1 == id_x1i && id_y1 == id_y1i)
											{
												int id_x2i = (int)floor(x2Inner / d);
												int id_y2i = (int)floor(y2Inner / d);
												party[0] -= party2[0];
												if (id_x1 == id_x2i)
													party[2] -= party2[1];
												else
													party[1] -= party2[1];

											}
											else if (id_x1 == id_x1i && id_y1 != id_y1i)
											{
												party[3] -= party2[1];
												party[2] -= party2[0];

											}
											else if (id_x1 != id_x1i && id_y1 == id_y1i)
											{
												party[3] -= party2[1];
												party[1] -= party2[0];
											}
										}
									}
								}
							}
							//for (int part = 0; part < jc2.size(); part++)
							//	deltasSpeeder[part][r][rt][t][u] = party[part];
						//}
						for (int part = 0; part < jc2.size(); part++)
						{
							Lc += party[part] * SpeederPartial[jc2[part] - predictedMinSize][kc2[part] - predictedMinSize][sqn - t][sqnsodd - u];
						}
						complex<double> tmp_u = (double)bc[sqnsodd][u] * Lc; // go back here
						sum_u += tmp_u;//deltasSpeeder[r][rt][t][u];
					}
					sum_t += tmp_t * sum_u;
				}
				M += tmp_s * sum_t;
			}
			M *= M_p[p];

			features[id] = abs(M);
		}
		return featuresCount;
	}

	/// <param name = 'featuresID'>Numery cech</param>
	/// <param name = 'Wx'>Szerokosc okna</param>
	/// <param name = 'Wy'>Wysokosc okna</param>
	/// <param name = 'xp'>X-pozycja okna (lewy gorny rog)</param>
	/// <param name = 'yp'>Y-pozycja okna (lewy gorny rog)</param>
	virtual int extractFromWindow(double* features, const int* featuresID, int fLength, int Wx, int Wy, int kp = 0, int jp = 0) override
	{
		if (fLength < 30)
			return extractFromWindowSmall(features, featuresID, fLength, Wx, Wy, kp, jp);
		else
		{
			complex<double>* M_p = M_p_w[Wx];

			//return extractFromWindow(Wx, Wy, xp, yp);
			double jc = jp + 1 + (Wx - 1) / 2.0;
			double kc = kp + 1 + (Wx - 1) / 2.0;
			int N = Wx;

			vector<vector<vector<vector<vector<complex<double>>>>>> deltasSpeeder(4,
				vector<vector<vector<vector<complex<double>>>>>(rings,
					vector<vector<vector<complex<double>>>>(ringsType + 1,
						vector<vector<complex<double>>>((p_max + q_max + 2) / 2,
							vector<complex<double>>((p_max + q_max + 2) / 2)))));

			vector<int> x1I(rings), y1I(rings), x2I(rings), y2I(rings);
			vector<int> x1_t(rings), y1_t(rings), x2_t(rings), y2_t(rings);
			vector<int> wc_t(rings);
			for (int r = 0; r < rings; r++)
			{
				int WInner = (int)round(N * sqrt((rings - (r + 1)) / (double)rings));
				if (WInner % 2 == 1)
					WInner++;
				x1I[r] = (int)(jc - 0.5 - WInner / 2);
				y1I[r] = (int)(kc - 0.5 - WInner / 2);
				x2I[r] = x1I[r] + WInner - 1;
				y2I[r] = y1I[r] + WInner - 1;

				int wc = (int)round(N*sqrt((rings - r) / (double)rings));
				if (wc % 2 == 1)
					wc++;
				wc_t[r] = wc;
				x1_t[r] = (int)(jc - 0.5 - wc / 2);
				y1_t[r] = (int)(kc - 0.5 - wc / 2);

				x2_t[r] = x1_t[r] + wc - 1;
				y2_t[r] = y1_t[r] + wc - 1;
			}

			for (int f = 0; f < fLength; f++)
			{
				int id = featuresID[f];
				const int &p = descriptions[id].p;
				const int &q = descriptions[id].q;
				const int &r = descriptions[id].r;
				const int &rt = descriptions[id].rt;

				double nwc = sqrt(2) / ((double)N);
				int x1Inner = 0, x2Inner = 0, y1Inner = 0, y2Inner = 0;
				if (r < rings - 1)
				{
					x1Inner = x1I[r];
					y1Inner = y1I[r];
					x2Inner = x2I[r];
					y2Inner = y2I[r];
				}

				vector<int> jc2;
				vector<int> kc2;
				int jo1 = (int)(x1_t[r] - floor(x1_t[r] / d)*d);
				int ko1 = (int)(y1_t[r] - floor(y1_t[r] / d)*d);
				jc2.push_back((int)((1.0 / 2 * (2 * jo1 + wc_t[r] - 1) - 0.5) + 1));
				kc2.push_back((int)((1.0 / 2 * (2 * ko1 + wc_t[r] - 1) - 0.5) + 1));
				if (jo1 + wc_t[r] > d)
				{
					jc2.push_back(jc2[0] - d);
					kc2.push_back(kc2[0]);
				}
				if (ko1 + wc_t[r] > d)
				{
					kc2.push_back(kc2[0] - d);
					jc2.push_back(jc2[0]);
				}
				if (jc2.size() == 3)
				{
					kc2.push_back(kc2[0] - d);
					jc2.push_back(jc2[0] - d);
				}

				complex<double> M = 0;
				for (int s = q; s <= p; s += 2)
				{
					double tmp_s = Beta[p][q][s] * powReal(nwc, s);
					complex<double> sum_t;
					int sqn = (int)((s - q) / 2.0);
					for (int t = 0; t <= sqn; t++)
					{
						complex<double> tmp_t = (double)bc[sqn][t];
						complex<double> sum_u;
						int sqnsodd = (int)((s + q) / 2.0);
						for (int u = 0; u <= sqnsodd; u++)
						{
							complex<double> sum(0, 0);
							complex<double> Lc(0.0);
							for (int part = 0; part < jc2.size(); part++)
							{
								sum += deltasSpeeder[part][r][rt][t][u];
							}
							if (sum == complex<double>(0.0))
							{
								vector<complex<double>> party = deltaPart(Integrals, d, x1_t[r], y1_t[r], (x2_t[r] - x1_t[r] + 1), t, u);
								if (r < rings - 1 && rt == 0)
								{
									vector<complex<double>> party2 = deltaPart(Integrals, d, x1Inner, y1Inner, abs(x2Inner - x1Inner + 1), t, u);
									if (party.size() == 1)
									{
										party[0] -= party2[0];
									}
									else if (party.size() == 2)
									{
										if (party2.size() == 2)
										{
											for (int part = 0; part < jc2.size(); part++)
												party[part] -= party2[part];
										}
										else
										{
											int id_x1 = (int)floor(jp / d);
											int id_x1i = (int)floor(x1Inner / d);
											int id_y1 = (int)floor(kp / d);
											int id_y1i = (int)floor(y1Inner / d);
											if (id_x1 == id_x1i && id_y1 == id_y1i)
											{
												party[0] -= party2[0];
											}
											else
												party[1] -= party2[0];
										}

									}
									else
									{
										if (party.size() == party2.size())
										{
											for (int part = 0; part < jc2.size(); part++)
												party[part] -= party2[part];
										}
										else
										{
											int id_x1 = (int)floor(jp / d);
											int id_x1i = (int)floor(x1Inner / d);
											int id_y1 = (int)floor(kp / d);
											int id_y1i = (int)floor(y1Inner / d);
											if (party2.size() == 1)
											{
												if (id_x1 == id_x1i && id_y1 == id_y1i)
												{
													party[0] -= party2[0];
												}
												else if (id_x1 == id_x1i && id_y1 != id_y1i)
												{
													party[2] -= party2[0];
												}
												else if (id_x1 != id_x1i && id_y1 == id_y1i)
												{
													party[1] -= party2[0];
												}
												else
												{
													party[3] -= party2[0];
												}
											}
											else if (party2.size() == 2)
											{
												if (id_x1 == id_x1i && id_y1 == id_y1i)
												{
													int id_x2i = (int)floor(x2Inner / d);
													int id_y2i = (int)floor(y2Inner / d);
													party[0] -= party2[0];
													if (id_x1 == id_x2i)
														party[2] -= party2[1];
													else
														party[1] -= party2[1];

												}
												else if (id_x1 == id_x1i && id_y1 != id_y1i)
												{
													party[3] -= party2[1];
													party[2] -= party2[0];

												}
												else if (id_x1 != id_x1i && id_y1 == id_y1i)
												{
													party[3] -= party2[1];
													party[1] -= party2[0];
												}
											}
										}
									}
								}
								for (int part = 0; part < jc2.size(); part++)
									deltasSpeeder[part][r][rt][t][u] = party[part];
							}
							for (int part = 0; part < jc2.size(); part++)
							{
								Lc += deltasSpeeder[part][r][rt][t][u] * SpeederPartial[jc2[part] - predictedMinSize][kc2[part] - predictedMinSize][sqn - t][sqnsodd - u];
							}
							complex<double> tmp_u = (double)bc[sqnsodd][u] * Lc; // go back here
							sum_u += tmp_u;//deltasSpeeder[r][rt][t][u];
						}
						sum_t += tmp_t * sum_u;
					}
					M += tmp_s * sum_t;
				}
				M *= M_p[p];

				features[id] = abs(M);
			}
			return featuresCount;
		}
	}

	/// <summary>Ekstrachuje cechy z podanego obrazu</summary>
	/// <param name = 'Wx'>Szerokosc okna</param>
	/// <param name = 'Wy'>Wysokosc okna</param>
	/// <param name = 'xp'>X-pozycja okna (lewy gorny rog)</param>
	/// <param name = 'yp'>Y-pozycja okna (lewy gorny rog)</param>
	/// <returns>Ekstrachowane cechy</returns>
	virtual tuple<int, const double*> extractFromWindow(int Wx, int Wy, int kp = 0, int jp = 0) override
	{
		double* features = new double[featuresCount];
		int id = -1;

		double npow2Local = (2.0 / pow(Wx, 2));
		double* M_p_Local = new double[p_max + 1];
		for (int p = 0; p <= p_max; p++)
			M_p_Local[p] = npow2Local / (pi2 * a[p]);

		vector<double> F;
		double jc = jp + 1 + (Wx - 1) / 2.0;
		double kc = kp + 1 + (Wx - 1) / 2.0;
		int N = Wx;

		vector<vector<vector<vector<vector<complex<double>>>>>> deltasSpeeder(4,
			vector<vector<vector<vector<complex<double>>>>>(rings,
				vector<vector<vector<complex<double>>>>(ringsType + 1,
					vector<vector<complex<double>>>((p_max + q_max + 2) / 2,
						vector<complex<double>>((p_max + q_max + 2) / 2)))));

		for (int r = 0; r < rings; r++)
		{
			int wc = (int)round(N*sqrt((rings - r) / (double)rings));
			if (wc % 2 == 1)
				wc++;
			int x1 = (int)(jc - 0.5 - wc / 2);
			int y1 = (int)(kc - 0.5 - wc / 2);

			int x2 = x1 + wc - 1;
			int y2 = y1 + wc - 1;

			vector<int> jc2;
			vector<int> kc2;
			int jo1 = (int)(x1 - floor(1.0*x1 / d)*d);
			int ko1 = (int)(y1 - floor(1.0*y1 / d)*d);

			jc2.push_back((int)((1.0 / 2 * (2 * jo1 + wc - 1) - 0.5) + 1));
			kc2.push_back((int)((1.0 / 2 * (2 * ko1 + wc - 1) - 0.5) + 1));

			if (jo1 + wc > d)
			{
				jc2.push_back(jc2[0] - d);
				kc2.push_back(kc2[0]);
			}
			if (ko1 + wc > d)
			{
				kc2.push_back(kc2[0] - d);
				jc2.push_back(jc2[0]);
			}
			if (jc2.size() == 3)
			{
				kc2.push_back(kc2[0] - d);
				jc2.push_back(jc2[0] - d);
			}

			double nwc = sqrt(2) / ((double)N);
			int x1Inner = 0, x2Inner = 0, y1Inner = 0, y2Inner = 0, WInner = 0;
			if (r < rings - 1)
			{
				WInner = (int)round(N * sqrt((rings - (r + 1)) / (double)rings));
				if (WInner % 2 == 1)
					WInner++;
				x1Inner = (int)(jc - 0.5 - WInner / 2);
				y1Inner = (int)(kc - 0.5 - WInner / 2);
				x2Inner = x1Inner + WInner - 1;
				y2Inner = y1Inner + WInner - 1;
			}

			int hMax = 0;
			if (ringsType == 1)
				hMax = (r == rings - 1) ? 0 : 1;

			for (int rt = 0; rt <= hMax; rt++)
			{
				for (int p = 0; p <= p_max; p++)
				{
					complex<double> M = M_p_Local[p];
					for (int q = p % 2; q <= min(p, q_max); q += 2)
					{
						complex<double> sum_s(0.0);
						for (int s = q; s <= p; s += 2)
						{
							double tmp_s = Beta[p][q][s] * powReal(nwc, s);
							complex<double> sum_t(0.0);
							int sqn = (int)((s - q) / 2.0);
							for (int t = 0; t <= sqn; t++)
							{
								complex<double> tmp_t = (double)bc[sqn][t];
								complex<double> sum_u(0.0);
								int sqnsodd = (int)((s + q) / 2.0);
								for (int u = 0; u <= sqnsodd; u++)
								{
									complex<double> sum(0, 0);
									complex<double> Lc(0.0);
									for (int part = 0; part < jc2.size(); part++)
									{
										sum += deltasSpeeder[part][r][rt][t][u];
									}
									if (sum == complex<double>(0.0))
									{
										vector<complex<double>> party = deltaPart(Integrals, d, x1, y1, (x2 - x1 + 1), t, u);
										if (r < rings - 1 && rt == 0)
										{
											vector<complex<double>> party2 = deltaPart(Integrals, d, x1Inner, y1Inner, abs(x2Inner - x1Inner + 1), t, u);
											if (party.size() == 1)
											{
												party[0] -= party2[0];
											}
											else if (party.size() == 2)
											{
												if (party2.size() == 2)
												{
													for (int part = 0; part < jc2.size(); part++)
														party[part] -= party2[part];
												}
												else
												{
													int id_x1 = (int)floor(jp / d);
													int id_x1i = (int)floor(x1Inner / d);
													int id_y1 = (int)floor(kp / d);
													int id_y1i = (int)floor(y1Inner / d);
													if (id_x1 == id_x1i && id_y1 == id_y1i)
													{
														party[0] -= party2[0];
													}
													else
														party[1] -= party2[0];
												}

											}
											else
											{
												if (party.size() == party2.size())
												{
													for (int part = 0; part < jc2.size(); part++)
														party[part] -= party2[part];
												}
												else
												{
													int id_x1 = (int)floor(jp / d);
													int id_x1i = (int)floor(x1Inner / d);
													int id_y1 = (int)floor(kp / d);
													int id_y1i = (int)floor(y1Inner / d);
													if (party2.size() == 1)
													{
														if (id_x1 == id_x1i && id_y1 == id_y1i)
														{
															party[0] -= party2[0];
														}
														else if (id_x1 == id_x1i && id_y1 != id_y1i)
														{
															party[2] -= party2[0];
														}
														else if (id_x1 != id_x1i && id_y1 == id_y1i)
														{
															party[1] -= party2[0];
														}
														else
														{
															party[3] -= party2[0];
														}
													}
													else if (party2.size() == 2)
													{
														if (id_x1 == id_x1i && id_y1 == id_y1i)
														{
															int id_x2i = (int)floor(x2Inner / d);
															int id_y2i = (int)floor(y2Inner / d);
															party[0] -= party2[0];
															if (id_x1 == id_x2i)
																party[2] -= party2[1];
															else
																party[1] -= party2[1];

														}
														else if (id_x1 == id_x1i && id_y1 != id_y1i)
														{
															party[3] -= party2[1];
															party[2] -= party2[0];

														}
														else if (id_x1 != id_x1i && id_y1 == id_y1i)
														{
															party[3] -= party2[1];
															party[1] -= party2[0];
														}
													}
												}
											}
										}
										for (int part = 0; part < jc2.size(); part++)
											deltasSpeeder[part][r][rt][t][u] = party[part];
									}
									for (int part = 0; part < jc2.size(); part++)
									{
										Lc += deltasSpeeder[part][r][rt][t][u] * SpeederPartial[jc2[part] - predictedMinSize][kc2[part] - predictedMinSize][sqn - t][sqnsodd - u];
									}
									complex<double> tmp_u = (double)bc[sqnsodd][u] * Lc; // go back here
									sum_u += tmp_u;//deltasSpeeder[r][rt][t][u];
								}
								sum_t += tmp_t * sum_u;
							}
							sum_s += tmp_s * sum_t;
						}
						complex<double> cm = M * sum_s;
						features[++id] = abs(M * sum_s);
					}
				}
			}
		}
		delete[] M_p_Local;

		return make_tuple(featuresCount, features);
	}

	/// <summary>Ekstrachuje cechy z podanych plikow</summary>
	/// <param name = 'paths'>Wektor sciezek do plikow</param>
	virtual tuple<int, int, const double* const*> extractMultipleFeatures(const vector<string> &paths)  override
	{
		const double ** X = new const double*[paths.size()];


#pragma omp parallel for num_threads(OMP_NUM_THR)
		for (int i = 0; i < (int)paths.size(); i++)
		{
			ZernikeFPII zernike(this);

			auto[fc, fets] = zernike.extractFeatures(paths[i]);
			X[i] = fets;
		}
		return make_tuple((int)paths.size(), featuresCount, X);
	}
};
vector<vector<vector<vector<complex<double>>>>> ZernikeFPII::SpeederPartial = vector<vector<vector<vector<complex<double>>>>>();
vector<vector<unsigned long long>> ZernikeFPII::bc = vector<vector<unsigned long long int>>();
vector<vector<vector<long long>>> ZernikeFPII::Beta = vector<vector<vector<long long>>>();
vector<double> ZernikeFPII::a = vector<double>();
bool ZernikeFPII::initialized = false;

class ZernikeFPIIinvariants : public ZernikeFPII
{
private:
	struct FeatureDescriptor
	{
		int r;
		int rt;
		int k;
		int p;
		int q;
		int v;
		int s;
		int kind;
	};

	vector<FeatureDescriptor> descriptions;

	ZernikeFPIIinvariants(ZernikeFPIIinvariants * parent) : ZernikeFPII(parent), descriptions(parent->descriptions)
	{
		this->featuresCount = parent->featuresCount;
	}
public:
	using Extractor::extractFromWindow;

	~ZernikeFPIIinvariants()
	{
		clearImageData();
	}

	ZernikeFPIIinvariants() : ZernikeFPIIinvariants(8, 8, 6, 0, 100, SaveFileType::text) {}

	ZernikeFPIIinvariants(const int p_max, const int q_max, const int rings, const int ringsType, const int d, SaveFileType fileType = SaveFileType::text)
		:ZernikeFPII(p_max, q_max, rings, ringsType, d, fileType)
	{
		for (int r = 0; r < rings; r++)
		{
			int hMax = 0;
			if (ringsType == 1)
				hMax = (r == rings - 1) ? 0 : 1;

			for (int rt = 0; rt <= hMax; rt++)
			{
				descriptions.push_back(FeatureDescriptor{ r, rt, 0, 0, 0, 0, 0, 0 });

				for (int k = 1; k <= p_max; k++)
				{
					int q_min = 1;
					if (k == 1)  q_min = 0;
					for (int q = q_min; q <= q_max; q++)
					{
						int s = k * q;
						for (int p = q; p <= p_max; p += 2)
						{
							int v_min = s;
							if (k == 1)
							{
								v_min = p;
								if (q == 0)
									v_min = p + 2;
							}
							for (int v = v_min; v <= q_max; v += 2)
							{
								descriptions.push_back(FeatureDescriptor{ r, rt, k, p, q, v, s, 0 });
								if (!((k == 1 && p == v) || q == 0))
									descriptions.push_back(FeatureDescriptor{ r, rt, k, p, q, v, s, 1 });
							}
						}
					}
				}
			}
		}
		featuresCount = (int)descriptions.size();
	}

	static string GetType()
	{
		return "ZernikeFPiiInvariantsExtractor";
	}

	/// <summary>Zwraca typ cechy</summary>
	/// <returns>Typ klasyfikatora</returns>
	string getType() const override
	{
		return GetType();
	}

	bool getRectangleWindowsRequirement() const override
	{
		return true;
	}

	/// <summary>Zwraca opis ekstraktora cech</summary>
	/// <returns>Opis ekstraktora cech</returns>
	string toString() const override
	{
		string text = getType() + "\r\n";
		text += "Pmax: " + to_string(p_max) + "\r\n";
		text += "Qmax: " + to_string(q_max) + "\r\n";
		text += "Rings: " + to_string(rings) + "\r\n";
		text += "RingsType: " + to_string(ringsType) + "\r\n";
		text += "Width: " + to_string(d) + "\r\n";

		return text;
	}

	void loadImageData(const string path) override
	{
		clearImageData();
		auto[height, width, img] = loadImage(path, saveMode);
		this->width = width;
		this->height = height;

		calculateII2(img, d);

		for (int i = 0; i < height; i++)
			delete[] img[i];
		delete[] img;
	}

	void loadImageData(const double* const* img, int height, int width) override
	{
		clearImageData();
		this->width = width;
		this->height = height;

		calculateII2(img, d);
	}

	//void clearImageData() override
	//{
	//	Integrals.clear();
	//}

	void initializeExtractor(Point* sizes, int sca) override
	{
		if (!initialized)
			ZernikeFPII::initializeExtractor();

		for (int s1 = 0; s1 < sca; s1++)
		{
			int Wx = sizes[s1].wx;

			if (M_p_w.count(Wx) == 0)
			{
				this->M_p_w[Wx] = new complex<double>[p_max + 1];
				for (int p = 0; p <= p_max; p++)
					this->M_p_w[Wx][p] = (2.0 / pow(Wx, 2)) / (pi2 * a[p]);
			}
		}
	}

	virtual int extractFromWindowSmall(double* features, const int* featuresID, int fLength, int Wx, int Wy, int kp = 0, int jp = 0)
	{
		complex<double>* M_p = M_p_w[Wx];

		double jc = jp + 1 + (Wx - 1) / 2.0;
		double kc = kp + 1 + (Wx - 1) / 2.0;
		int N = Wx;

		vector<vector<vector<vector<complex<double>>>>> oldMoments(rings,
			vector<vector<vector<complex<double>>>>(ringsType + 1,
				vector<vector<complex<double>>>(p_max + 1,
					vector<complex<double>>(q_max + 1))));

		vector<int> x1I(rings), y1I(rings), x2I(rings), y2I(rings);
		vector<int> x1_t(rings), y1_t(rings), x2_t(rings), y2_t(rings);
		vector<int> wc_t(rings);

		vector<vector<int>> jc2(rings);
		vector<vector<int>> kc2(rings);
		//vector<vector<pair<int, int>>> odejmowanko(rings);
		for (int r = 0; r < rings; r++)
		{
			int WInner = (int)round(N * sqrt((rings - (r + 1)) / (double)rings));
			if (WInner % 2 == 1)
				WInner++;
			x1I[r] = (int)(jc - 0.5 - WInner / 2);
			y1I[r] = (int)(kc - 0.5 - WInner / 2);
			x2I[r] = x1I[r] + WInner - 1;
			y2I[r] = y1I[r] + WInner - 1;

			int wc = (int)round(N*sqrt((rings - r) / (double)rings));
			if (wc % 2 == 1)
				wc++;
			wc_t[r] = wc;
			x1_t[r] = (int)(jc - 0.5 - wc / 2);
			y1_t[r] = (int)(kc - 0.5 - wc / 2);

			x2_t[r] = x1_t[r] + wc - 1;
			y2_t[r] = y1_t[r] + wc - 1;

			//errfile << "wyznaczanie srodkow" << endl;

			int jo1 = (int)(x1_t[r] - floor(x1_t[r] / d)*d);
			int ko1 = (int)(y1_t[r] - floor(y1_t[r] / d)*d);
			jc2[r].push_back((int)((1.0 / 2 * (2 * jo1 + wc_t[r] - 1) - 0.5) + 1));
			kc2[r].push_back((int)((1.0 / 2 * (2 * ko1 + wc_t[r] - 1) - 0.5) + 1));
			if (jo1 + wc_t[r] > d)
			{
				jc2[r].push_back(jc2[r][0] - d);
				kc2[r].push_back(kc2[r][0]);
			}
			if (ko1 + wc_t[r] > d)
			{
				kc2[r].push_back(kc2[r][0] - d);
				jc2[r].push_back(jc2[r][0]);
			}
			if (jc2[r].size() == 3)
			{
				kc2[r].push_back(kc2[r][0] - d);
				jc2[r].push_back(jc2[r][0] - d);
			}
		}


		double nwc = sqrt(2) / ((double)N);
		//errfile << "teraz cechy" << endl;
		for (int f = 0; f < fLength; f++)
		{
			int id = featuresID[f];
			//errfile << id << endl;
			int p = descriptions[id].p;
			int q = descriptions[id].q;
			const int &r = descriptions[id].r;
			const int &rt = descriptions[id].rt;

			//errfile << "pierwsza cechy" << endl;
			if (oldMoments[r][rt][p][q] == 0.0)
			{
				for (int s = q; s <= p; s += 2)
				{
					double tmp_s = Beta[p][q][s] * powReal(nwc, s);
					complex<double> sum_t;
					int sqn = (int)((s - q) / 2.0);
					for (int t = 0; t <= sqn; t++)
					{
						complex<double> tmp_t = (double)bc[sqn][t];
						complex<double> sum_u;
						int sqnsodd = (int)((s + q) / 2.0);
						for (int u = 0; u <= sqnsodd; u++)
						{
							complex<double> sum(0, 0);
							complex<double> Lc(0.0);
							//for (int part = 0; part < jc2[r].size(); part++)
							//{
							//	sum += deltasSpeeder[part][r][rt][t][u];
							//}
							//if (sum == complex<double>(0.0))
							//{
								vector<complex<double>> party = deltaPart(Integrals, d, x1_t[r], y1_t[r], (x2_t[r] - x1_t[r] + 1), t, u);
								if (r < rings - 1 && rt == 0)
								{
									vector<complex<double>> party2 = deltaPart(Integrals, d, x1I[r], y1I[r], abs(x2I[r] - x1I[r] + 1), t, u);
									//for (int i = 0; i < odejmowanko[r].size(); i++)
									//	party[odejmowanko[r][i].first] -= party2[odejmowanko[r][i].second];
									if (party.size() == 1)
									{
										party[0] -= party2[0];
									}
									else if (party.size() == 2)
									{
										if (party2.size() == 2)
										{
											for (int part = 0; part < jc2[r].size(); part++)
												party[part] -= party2[part];
										}
										else
										{
											int id_x1 = (int)floor(jp / d);
											int id_x1i = (int)floor(x1I[r] / d);
											int id_y1 = (int)floor(kp / d);
											int id_y1i = (int)floor(y1I[r] / d);
											if (id_x1 == id_x1i && id_y1 == id_y1i)
											{
												party[0] -= party2[0];
											}
											else
												party[1] -= party2[0];
										}

									}
									else
									{
										if (party.size() == party2.size())
										{
											for (int part = 0; part < jc2[r].size(); part++)
												party[part] -= party2[part];
										}
										else
										{
											int id_x1 = (int)floor(jp / d);
											int id_x1i = (int)floor(x1I[r] / d);
											int id_y1 = (int)floor(kp / d);
											int id_y1i = (int)floor(y1I[r] / d);
											if (party2.size() == 1)
											{
												if (id_x1 == id_x1i && id_y1 == id_y1i)
												{
													party[0] -= party2[0];
												}
												else if (id_x1 == id_x1i && id_y1 != id_y1i)
												{
													party[2] -= party2[0];
												}
												else if (id_x1 != id_x1i && id_y1 == id_y1i)
												{
													party[1] -= party2[0];
												}
												else
												{
													party[3] -= party2[0];
												}
											}
											else if (party2.size() == 2)
											{
												if (id_x1 == id_x1i && id_y1 == id_y1i)
												{
													int id_x2i = (int)floor(x2I[r] / d);
													int id_y2i = (int)floor(y2I[r] / d);
													party[0] -= party2[0];
													if (id_x1 == id_x2i)
														party[2] -= party2[1];
													else
														party[1] -= party2[1];

												}
												else if (id_x1 == id_x1i && id_y1 != id_y1i)
												{
													party[3] -= party2[1];
													party[2] -= party2[0];

												}
												else if (id_x1 != id_x1i && id_y1 == id_y1i)
												{
													party[3] -= party2[1];
													party[1] -= party2[0];
												}
											}
										}
									}

								}
								//for (int part = 0; part < jc2[r].size(); part++)
								//	deltasSpeeder[part][r][rt][t][u] = party[part];
							//}
							for (int part = 0; part < jc2[r].size(); part++)
							{
								Lc += party[part] * SpeederPartial[jc2[r][part] - predictedMinSize][kc2[r][part] - predictedMinSize][sqn - t][sqnsodd - u];
							}
							complex<double> tmp_u = (double)bc[sqnsodd][u] * Lc; // go back here
							sum_u += tmp_u;//deltasSpeeder[r][rt][t][u];
						}
						sum_t += tmp_t * sum_u;
					}
					oldMoments[r][rt][p][q] += tmp_s * sum_t;
				}
				oldMoments[r][rt][p][q] *= M_p[p];
			}

			//errfile << "druga cechy" << endl;
			p = descriptions[id].v;
			q = descriptions[id].s;
			if (oldMoments[r][rt][p][q] == 0.0)
			{
				for (int s = q; s <= p; s += 2)
				{
					double tmp_s = Beta[p][q][s] * powReal(nwc, s);
					complex<double> sum_t;
					int sqn = (int)((s - q) / 2.0);
					for (int t = 0; t <= sqn; t++)
					{
						complex<double> tmp_t = (double)bc[sqn][t];
						complex<double> sum_u;
						int sqnsodd = (int)((s + q) / 2.0);
						for (int u = 0; u <= sqnsodd; u++)
						{
							complex<double> sum(0, 0);
							complex<double> Lc(0.0);
							//for (int part = 0; part < jc2[r].size(); part++)
							//{
							//	sum += deltasSpeeder[part][r][rt][t][u];
							//}
							//if (sum == complex<double>(0.0))
							//{
								vector<complex<double>> party = deltaPart(Integrals, d, x1_t[r], y1_t[r], (x2_t[r] - x1_t[r] + 1), t, u);
								if (r < rings - 1 && rt == 0)
								{
									vector<complex<double>> party2 = deltaPart(Integrals, d, x1I[r], y1I[r], abs(x2I[r] - x1I[r] + 1), t, u);
									//for (int i = 0; i < odejmowanko[r].size(); i++)
									//	party[odejmowanko[r][i].first] -= party2[odejmowanko[r][i].second];
									if (party.size() == 1)
									{
										party[0] -= party2[0];
									}
									else if (party.size() == 2)
									{
										if (party2.size() == 2)
										{
											for (int part = 0; part < jc2[r].size(); part++)
												party[part] -= party2[part];
										}
										else
										{
											int id_x1 = (int)floor(jp / d);
											int id_x1i = (int)floor(x1I[r] / d);
											int id_y1 = (int)floor(kp / d);
											int id_y1i = (int)floor(y1I[r] / d);
											if (id_x1 == id_x1i && id_y1 == id_y1i)
											{
												party[0] -= party2[0];
											}
											else
												party[1] -= party2[0];
										}

									}
									else
									{
										if (party.size() == party2.size())
										{
											for (int part = 0; part < jc2[r].size(); part++)
												party[part] -= party2[part];
										}
										else
										{
											int id_x1 = (int)floor(jp / d);
											int id_x1i = (int)floor(x1I[r] / d);
											int id_y1 = (int)floor(kp / d);
											int id_y1i = (int)floor(y1I[r] / d);
											if (party2.size() == 1)
											{
												if (id_x1 == id_x1i && id_y1 == id_y1i)
												{
													party[0] -= party2[0];
												}
												else if (id_x1 == id_x1i && id_y1 != id_y1i)
												{
													party[2] -= party2[0];
												}
												else if (id_x1 != id_x1i && id_y1 == id_y1i)
												{
													party[1] -= party2[0];
												}
												else
												{
													party[3] -= party2[0];
												}
											}
											else if (party2.size() == 2)
											{
												if (id_x1 == id_x1i && id_y1 == id_y1i)
												{
													int id_x2i = (int)floor(x2I[r] / d);
													int id_y2i = (int)floor(y2I[r] / d);
													party[0] -= party2[0];
													if (id_x1 == id_x2i)
														party[2] -= party2[1];
													else
														party[1] -= party2[1];

												}
												else if (id_x1 == id_x1i && id_y1 != id_y1i)
												{
													party[3] -= party2[1];
													party[2] -= party2[0];

												}
												else if (id_x1 != id_x1i && id_y1 == id_y1i)
												{
													party[3] -= party2[1];
													party[1] -= party2[0];
												}
											}
										}
									}

								}
								//for (int part = 0; part < jc2[r].size(); part++)
								//	deltasSpeeder[part][r][rt][t][u] = party[part];
							//}
							for (int part = 0; part < jc2[r].size(); part++)
							{
								//Lc += party[part] * SpeederPartial[jc2[r][part] - predictedMinSize][kc2[r][part] - predictedMinSize][sqn - t][sqnsodd - u];
							}
							complex<double> tmp_u = (double)bc[sqnsodd][u] * Lc; // go back here
							sum_u += tmp_u;//deltasSpeeder[r][rt][t][u];
						}
						sum_t += tmp_t * sum_u;
					}
					oldMoments[r][rt][p][q] += tmp_s * sum_t;
				}
				oldMoments[r][rt][p][q] *= M_p[p];
			}
		}

		//errfile << "mnozenie" << endl;
		for (int f = 0; f < fLength; f++)
		{
			int id = featuresID[f];
			//errfile << id << endl;
			const int &p = descriptions[id].p;
			const int &q = descriptions[id].q;
			const int &v = descriptions[id].v;
			const int &qq = descriptions[id].s;
			const int &r = descriptions[id].r;
			const int &rt = descriptions[id].rt;
			const int &k = descriptions[id].k;
			const int &kind = descriptions[id].kind;

			complex<double> c = pow(oldMoments[r][rt][p][q], k)*conj(oldMoments[r][rt][v][qq]);
			if (kind == 0) features[id] = real(c);
			else features[id] = imag(c);
		}
		return featuresCount;
	}

	/// <param name = 'featuresID'>Numery cech</param>
	/// <param name = 'Wx'>Szerokosc okna</param>
	/// <param name = 'Wy'>Wysokosc okna</param>
	/// <param name = 'xp'>X-pozycja okna (lewy gorny rog)</param>
	/// <param name = 'yp'>Y-pozycja okna (lewy gorny rog)</param>
	virtual int extractFromWindow(double* features, const int* featuresID, int fLength, int Wx, int Wy, int kp = 0, int jp = 0) override
	{
		if (fLength < 30)
		{
			return extractFromWindowSmall(features, featuresID, fLength, Wx, Wy, kp, jp);
		}
		else
		{
			complex<double>* M_p = M_p_w[Wx];

			double jc = jp + 1 + (Wx - 1) / 2.0;
			double kc = kp + 1 + (Wx - 1) / 2.0;
			int N = Wx;

			vector<vector<vector<vector<vector<complex<double>>>>>> deltasSpeeder(4,
				vector<vector<vector<vector<complex<double>>>>>(rings,
					vector<vector<vector<complex<double>>>>(ringsType + 1,
						vector<vector<complex<double>>>((p_max + q_max + 2) / 2,
							vector<complex<double>>((p_max + q_max + 2) / 2)))));

			vector<vector<vector<vector<complex<double>>>>> oldMoments(rings,
				vector<vector<vector<complex<double>>>>(ringsType + 1,
					vector<vector<complex<double>>>(p_max + 1,
						vector<complex<double>>(q_max + 1))));

			vector<int> x1I(rings), y1I(rings), x2I(rings), y2I(rings);
			vector<int> x1_t(rings), y1_t(rings), x2_t(rings), y2_t(rings);
			vector<int> wc_t(rings);

			vector<vector<int>> jc2(rings);
			vector<vector<int>> kc2(rings);
			for (int r = 0; r < rings; r++)
			{
				int WInner = (int)round(N * sqrt((rings - (r + 1)) / (double)rings));
				if (WInner % 2 == 1)
					WInner++;
				x1I[r] = (int)(jc - 0.5 - WInner / 2);
				y1I[r] = (int)(kc - 0.5 - WInner / 2);
				x2I[r] = x1I[r] + WInner - 1;
				y2I[r] = y1I[r] + WInner - 1;

				int wc = (int)round(N*sqrt((rings - r) / (double)rings));
				if (wc % 2 == 1)
					wc++;
				wc_t[r] = wc;
				x1_t[r] = (int)(jc - 0.5 - wc / 2);
				y1_t[r] = (int)(kc - 0.5 - wc / 2);

				x2_t[r] = x1_t[r] + wc - 1;
				y2_t[r] = y1_t[r] + wc - 1;

				int jo1 = (int)(x1_t[r] - floor(x1_t[r] / d)*d);
				int ko1 = (int)(y1_t[r] - floor(y1_t[r] / d)*d);
				jc2[r].push_back((int)((1.0 / 2 * (2 * jo1 + wc_t[r] - 1) - 0.5) + 1));
				kc2[r].push_back((int)((1.0 / 2 * (2 * ko1 + wc_t[r] - 1) - 0.5) + 1));
				if (jo1 + wc_t[r] > d)
				{
					jc2[r].push_back(jc2[r][0] - d);
					kc2[r].push_back(kc2[r][0]);
				}
				if (ko1 + wc_t[r] > d)
				{
					kc2[r].push_back(kc2[r][0] - d);
					jc2[r].push_back(jc2[r][0]);
				}
				if (jc2[r].size() == 3)
				{
					kc2[r].push_back(kc2[r][0] - d);
					jc2[r].push_back(jc2[r][0] - d);
				}

			
			}

			const double nwc = sqrt(2) / ((double)N);
			for (int f = 0; f < fLength; f++)
			{
				const int id = featuresID[f];
				int p = descriptions[id].p;
				int q = descriptions[id].q;
				const int& r = descriptions[id].r;
				const int& rt = descriptions[id].rt;

				int x1Inner = 0, x2Inner = 0, y1Inner = 0, y2Inner = 0;
				if (r < rings - 1)
				{
					x1Inner = x1I[r];
					y1Inner = y1I[r];
					x2Inner = x2I[r];
					y2Inner = y2I[r];
				}

				//errfile << "pierwsza cechy" << endl;
				if (oldMoments[r][rt][p][q] == complex<double>(0.0))
				{
					for (int s = q; s <= p; s += 2)
					{
						double tmp_s = Beta[p][q][s] * powReal(nwc, s);
						complex<double> sum_t;
						int sqn = (int)((s - q) / 2.0);
						for (int t = 0; t <= sqn; t++)
						{
							complex<double> tmp_t = (double)bc[sqn][t];
							complex<double> sum_u;
							int sqnsodd = (int)((s + q) / 2.0);
							for (int u = 0; u <= sqnsodd; u++)
							{
								complex<double> sum(0, 0);
								complex<double> Lc(0.0);
								for (int part = 0; part < jc2[r].size(); part++)
								{
									sum += deltasSpeeder[part][r][rt][t][u];
								}
								if (sum == complex<double>(0.0))
								{
									vector<complex<double>> party = deltaPart(Integrals, d, x1_t[r], y1_t[r], (x2_t[r] - x1_t[r] + 1), t, u);
									if (r < rings - 1 && rt == 0)
									{
										vector<complex<double>> party2 = deltaPart(Integrals, d, x1Inner, y1Inner, abs(x2Inner - x1Inner + 1), t, u);
										if (party.size() == 1)
										{
											party[0] -= party2[0];
										}
										else if (party.size() == 2)
										{
											if (party2.size() == 2)
											{
												for (int part = 0; part < jc2[r].size(); part++)
													party[part] -= party2[part];
											}
											else
											{
												int id_x1 = (int)floor(jp / d);
												int id_x1i = (int)floor(x1Inner / d);
												int id_y1 = (int)floor(kp / d);
												int id_y1i = (int)floor(y1Inner / d);
												if (id_x1 == id_x1i && id_y1 == id_y1i)
												{
													party[0] -= party2[0];
												}
												else
													party[1] -= party2[0];
											}

										}
										else
										{
											if (party.size() == party2.size())
											{
												for (int part = 0; part < jc2[r].size(); part++)
													party[part] -= party2[part];
											}
											else
											{
												int id_x1 = (int)floor(jp / d);
												int id_x1i = (int)floor(x1Inner / d);
												int id_y1 = (int)floor(kp / d);
												int id_y1i = (int)floor(y1Inner / d);
												if (party2.size() == 1)
												{
													if (id_x1 == id_x1i && id_y1 == id_y1i)
													{
														party[0] -= party2[0];
													}
													else if (id_x1 == id_x1i && id_y1 != id_y1i)
													{
														party[2] -= party2[0];
													}
													else if (id_x1 != id_x1i && id_y1 == id_y1i)
													{
														party[1] -= party2[0];
													}
													else
													{
														party[3] -= party2[0];
													}
												}
												else if (party2.size() == 2)
												{
													if (id_x1 == id_x1i && id_y1 == id_y1i)
													{
														int id_x2i = (int)floor(x2I[r] / d);
														int id_y2i = (int)floor(y2I[r] / d);
														party[0] -= party2[0];
														if (id_x1 == id_x2i)
															party[2] -= party2[1];
														else
															party[1] -= party2[1];

													}
													else if (id_x1 == id_x1i && id_y1 != id_y1i)
													{
														party[3] -= party2[1];
														party[2] -= party2[0];

													}
													else if (id_x1 != id_x1i && id_y1 == id_y1i)
													{
														party[3] -= party2[1];
														party[1] -= party2[0];
													}
												}
											}
										}

									}
									for (int part = 0; part < jc2[r].size(); part++)
										deltasSpeeder[part][r][rt][t][u] = party[part];
								}
								for (int part = 0; part < jc2[r].size(); part++)
								{
									Lc += deltasSpeeder[part][r][rt][t][u] * SpeederPartial[jc2[r][part] - predictedMinSize][kc2[r][part] - predictedMinSize][sqn - t][sqnsodd - u];
								}
								complex<double> tmp_u = (double)bc[sqnsodd][u] * Lc; // go back here
								sum_u += tmp_u;//deltasSpeeder[r][rt][t][u];
							}
							sum_t += tmp_t * sum_u;
						}
						oldMoments[r][rt][p][q] += tmp_s * sum_t;
					}
					oldMoments[r][rt][p][q] *= M_p[p];
				}

				//errfile << "druga cechy" << endl;
				p = descriptions[id].v;
				q = descriptions[id].s;
				if (oldMoments[r][rt][p][q] == complex<double>(0.0))
				{
					for (int s = q; s <= p; s += 2)
					{
						double tmp_s = Beta[p][q][s] * powReal(nwc, s);
						complex<double> sum_t;
						int sqn = (int)((s - q) / 2.0);
						for (int t = 0; t <= sqn; t++)
						{
							complex<double> tmp_t = (double)bc[sqn][t];
							complex<double> sum_u;
							int sqnsodd = (int)((s + q) / 2.0);
							for (int u = 0; u <= sqnsodd; u++)
							{
								complex<double> sum(0, 0);
								complex<double> Lc(0.0);
								for (int part = 0; part < jc2[r].size(); part++)
								{
									sum += deltasSpeeder[part][r][rt][t][u];
								}
								if (sum == complex<double>(0.0))
								{
									vector<complex<double>> party = deltaPart(Integrals, d, x1_t[r], y1_t[r], (x2_t[r] - x1_t[r] + 1), t, u);
									if (r < rings - 1 && rt == 0)
									{
										vector<complex<double>> party2 = deltaPart(Integrals, d, x1Inner, y1Inner, abs(x2Inner - x1Inner + 1), t, u);
										if (party.size() == 1)
										{
											party[0] -= party2[0];
										}
										else if (party.size() == 2)
										{
											if (party2.size() == 2)
											{
												for (int part = 0; part < jc2[r].size(); part++)
													party[part] -= party2[part];
											}
											else
											{
												int id_x1 = (int)floor(jp / d);
												int id_x1i = (int)floor(x1Inner / d);
												int id_y1 = (int)floor(kp / d);
												int id_y1i = (int)floor(y1Inner / d);
												if (id_x1 == id_x1i && id_y1 == id_y1i)
												{
													party[0] -= party2[0];
												}
												else
													party[1] -= party2[0];
											}

										}
										else
										{
											if (party.size() == party2.size())
											{
												for (int part = 0; part < jc2[r].size(); part++)
													party[part] -= party2[part];
											}
											else
											{
												int id_x1 = (int)floor(jp / d);
												int id_x1i = (int)floor(x1Inner / d);
												int id_y1 = (int)floor(kp / d);
												int id_y1i = (int)floor(y1Inner / d);
												if (party2.size() == 1)
												{
													if (id_x1 == id_x1i && id_y1 == id_y1i)
													{
														party[0] -= party2[0];
													}
													else if (id_x1 == id_x1i && id_y1 != id_y1i)
													{
														party[2] -= party2[0];
													}
													else if (id_x1 != id_x1i && id_y1 == id_y1i)
													{
														party[1] -= party2[0];
													}
													else
													{
														party[3] -= party2[0];
													}
												}
												else if (party2.size() == 2)
												{
													if (id_x1 == id_x1i && id_y1 == id_y1i)
													{
														int id_x2i = (int)floor(x2I[r] / d);
														int id_y2i = (int)floor(y2I[r] / d);
														party[0] -= party2[0];
														if (id_x1 == id_x2i)
															party[2] -= party2[1];
														else
															party[1] -= party2[1];

													}
													else if (id_x1 == id_x1i && id_y1 != id_y1i)
													{
														party[3] -= party2[1];
														party[2] -= party2[0];

													}
													else if (id_x1 != id_x1i && id_y1 == id_y1i)
													{
														party[3] -= party2[1];
														party[1] -= party2[0];
													}
												}
											}
										}

									}
									for (int part = 0; part < jc2[r].size(); part++)
										deltasSpeeder[part][r][rt][t][u] = party[part];
								}
								for (int part = 0; part < jc2[r].size(); part++)
								{
									Lc += deltasSpeeder[part][r][rt][t][u] * SpeederPartial[jc2[r][part] - predictedMinSize][kc2[r][part] - predictedMinSize][sqn - t][sqnsodd - u];
								}
								complex<double> tmp_u = (double)bc[sqnsodd][u] * Lc; // go back here
								sum_u += tmp_u;//deltasSpeeder[r][rt][t][u];
							}
							sum_t += tmp_t * sum_u;
						}
						oldMoments[r][rt][p][q] += tmp_s * sum_t;
					}
					oldMoments[r][rt][p][q] *= M_p[p];
				}

				int v = descriptions[id].v;
				int qq = descriptions[id].s;
				int k = descriptions[id].k;
				int kind = descriptions[id].kind;
				p = descriptions[id].p;
				q = descriptions[id].q;				

				complex<double> c = pow(oldMoments[r][rt][p][q], k) * conj(oldMoments[r][rt][v][qq]);
				if (kind == 0) features[id] = real(c);
				else features[id] = imag(c);
			}
			return featuresCount;
		}
	}

	/// <summary>Ekstrachuje cechy z podanego obrazu</summary>
	/// <param name = 'Wx'>Szerokosc okna</param>
	/// <param name = 'Wy'>Wysokosc okna</param>
	/// <param name = 'xp'>X-pozycja okna (lewy gorny rog)</param>
	/// <param name = 'yp'>Y-pozycja okna (lewy gorny rog)</param>
	/// <returns>Ekstrachowane cechy</returns>
	virtual tuple<int, const double*> extractFromWindow(int Wx, int Wy, int kp = 0, int jp = 0) override
	{
		double* features = new double[featuresCount];
		int id = -1;

		double npow2Local = (2.0 / pow(Wx, 2));
		double* M_p_Local = new double[p_max + 1];
		for (int p = 0; p <= p_max; p++)
			M_p_Local[p] = npow2Local / (pi2 * a[p]);

		vector<double> F;
		double jc = jp + 1 + (Wx - 1) / 2.0;
		double kc = kp + 1 + (Wx - 1) / 2.0;
		int N = Wx;

		vector<vector<vector<vector<vector<complex<double>>>>>> deltasSpeeder(4,
			vector<vector<vector<vector<complex<double>>>>>(rings,
				vector<vector<vector<complex<double>>>>(ringsType + 1,
					vector<vector<complex<double>>>((p_max + q_max + 2) / 2,
						vector<complex<double>>((p_max + q_max + 2) / 2)))));

		vector<vector<vector<vector<complex<double>>>>> oldMoments(rings,
			vector<vector<vector<complex<double>>>>(ringsType + 1,
				vector<vector<complex<double>>>(p_max + 1,
					vector<complex<double>>(q_max + 1))));

		for (int r = 0; r < rings; r++)
		{
			int wc = (int)round(N*sqrt((rings - r) / (double)rings));
			if (wc % 2 == 1)
				wc++;
			int x1 = (int)(jc - 0.5 - wc / 2);
			int y1 = (int)(kc - 0.5 - wc / 2);

			int x2 = x1 + wc - 1;
			int y2 = y1 + wc - 1;

			vector<int> jc2;
			vector<int> kc2;
			int jo1 = (int)(x1 - floor(1.0*x1 / d)*d);
			int ko1 = (int)(y1 - floor(1.0*y1 / d)*d);

			jc2.push_back((int)((1.0 / 2 * (2 * jo1 + wc - 1) - 0.5) + 1));
			kc2.push_back((int)((1.0 / 2 * (2 * ko1 + wc - 1) - 0.5) + 1));

			if (jo1 + wc > d)
			{
				jc2.push_back(jc2[0] - d);
				kc2.push_back(kc2[0]);
			}
			if (ko1 + wc > d)
			{
				kc2.push_back(kc2[0] - d);
				jc2.push_back(jc2[0]);
			}
			if (jc2.size() == 3)
			{
				kc2.push_back(kc2[0] - d);
				jc2.push_back(jc2[0] - d);
			}

			double nwc = sqrt(2) / ((double)N);
			int x1Inner = 0, x2Inner = 0, y1Inner = 0, y2Inner = 0, WInner = 0;
			if (r < rings - 1)
			{
				WInner = (int)round(N * sqrt((rings - (r + 1)) / (double)rings));
				if (WInner % 2 == 1)
					WInner++;
				x1Inner = (int)(jc - 0.5 - WInner / 2);
				y1Inner = (int)(kc - 0.5 - WInner / 2);
				x2Inner = x1Inner + WInner - 1;
				y2Inner = y1Inner + WInner - 1;
			}

			int hMax = 0;
			if (ringsType == 1)
				hMax = (r == rings - 1) ? 0 : 1;

			for (int rt = 0; rt <= hMax; rt++)
			{
				for (int p = 0; p <= p_max; p++)
				{
					complex<double> M = M_p_Local[p];
					for (int q = p % 2; q <= min(p, q_max); q += 2)
					{
						complex<double> sum_s(0.0);
						for (int s = q; s <= p; s += 2)
						{
							double tmp_s = Beta[p][q][s] * powReal(nwc, s);
							complex<double> sum_t(0.0);
							int sqn = (int)((s - q) / 2.0);
							for (int t = 0; t <= sqn; t++)
							{
								complex<double> tmp_t = (double)bc[sqn][t];
								complex<double> sum_u(0.0);
								int sqnsodd = (int)((s + q) / 2.0);
								for (int u = 0; u <= sqnsodd; u++)
								{
									complex<double> sum(0, 0);
									complex<double> Lc(0.0);
									for (int part = 0; part < jc2.size(); part++)
									{
										sum += deltasSpeeder[part][r][rt][t][u];
									}
									if (sum == complex<double>(0.0))
									{
										vector<complex<double>> party = deltaPart(Integrals, d, x1, y1, (x2 - x1 + 1), t, u);
										if (r < rings - 1 && rt == 0)
										{
											vector<complex<double>> party2 = deltaPart(Integrals, d, x1Inner, y1Inner, abs(x2Inner - x1Inner + 1), t, u);
											if (party.size() == 1)
											{
												party[0] -= party2[0];
											}
											else if (party.size() == 2)
											{
												if (party2.size() == 2)
												{
													for (int part = 0; part < jc2.size(); part++)
														party[part] -= party2[part];
												}
												else
												{
													int id_x1 = (int)floor(jp / d);
													int id_x1i = (int)floor(x1Inner / d);
													int id_y1 = (int)floor(kp / d);
													int id_y1i = (int)floor(y1Inner / d);
													if (id_x1 == id_x1i && id_y1 == id_y1i)
													{
														party[0] -= party2[0];
													}
													else
														party[1] -= party2[0];
												}

											}
											else
											{
												if (party.size() == party2.size())
												{
													for (int part = 0; part < jc2.size(); part++)
														party[part] -= party2[part];
												}
												else
												{
													/*if (r > 0)
													{
														cout << "(" << r << ", " << rt << ", " << p << ", " << q << ") " << endl;
														system("PAUSE");
													}*/
													int id_x1 = (int)floor(jp / d);
													int id_x1i = (int)floor(x1Inner / d);
													int id_y1 = (int)floor(kp / d);
													int id_y1i = (int)floor(y1Inner / d);
													if (party2.size() == 1)
													{
														if (id_x1 == id_x1i && id_y1 == id_y1i)
														{
															party[0] -= party2[0];
														}
														else if (id_x1 == id_x1i && id_y1 != id_y1i)
														{
															party[2] -= party2[0];
														}
														else if (id_x1 != id_x1i && id_y1 == id_y1i)
														{
															party[1] -= party2[0];
														}
														else
														{
															party[3] -= party2[0];
														}
													}
													else if (party2.size() == 2)
													{
														if (id_x1 == id_x1i && id_y1 == id_y1i)
														{
															int id_x2i = (int)floor(x2Inner / d);
															int id_y2i = (int)floor(y2Inner / d);
															party[0] -= party2[0];
															if (id_x1 == id_x2i)
																party[2] -= party2[1];
															else
																party[1] -= party2[1];

														}
														else if (id_x1 == id_x1i && id_y1 != id_y1i)
														{
															party[3] -= party2[1];
															party[2] -= party2[0];

														}
														else if (id_x1 != id_x1i && id_y1 == id_y1i)
														{
															party[3] -= party2[1];
															party[1] -= party2[0];
														}
													}
												}
											}
										}
										for (int part = 0; part < jc2.size(); part++)
											deltasSpeeder[part][r][rt][t][u] = party[part];
									}
									for (int part = 0; part < jc2.size(); part++)
									{
										Lc += deltasSpeeder[part][r][rt][t][u] * SpeederPartial[jc2[part] - predictedMinSize][kc2[part] - predictedMinSize][sqn - t][sqnsodd - u];
									}
									complex<double> tmp_u = (double)bc[sqnsodd][u] * Lc; // go back here
									sum_u += tmp_u;//deltasSpeeder[r][rt][t][u];
								}
								sum_t += tmp_t * sum_u;
							}
							sum_s += tmp_s * sum_t;
						}
						oldMoments[r][rt][p][q] = M * sum_s;
						//cout << "r: " << r << ", rt: " << rt << ", p: " << p << ", q: " << q << ", F: " << setw(16) << setprecision(10) << abs(oldMoments[r][rt][p][q]) << endl;
					}
				}
			}
		}

		for (int i = 0; i < descriptions.size(); i++)
		{
			int r = descriptions[i].r;
			int rt = descriptions[i].rt;
			int k = descriptions[i].k;
			int p = descriptions[i].p;
			int q = descriptions[i].q;
			int v = descriptions[i].v;
			int s = descriptions[i].s;
			int kind = descriptions[i].kind;
			complex<double> c1 = oldMoments[r][rt][p][q];
			complex<double> c2 = oldMoments[r][rt][v][s];
			cout << "r: " << r << ", rt: " << rt << ", p: " << p << ", q: " << q << ", F: " << setw(16) << setprecision(10) << c1 << endl;
			cout << "r: " << r << ", rt: " << rt << ", v: " << v << ", s: " << s << ", F: " << setw(16) << setprecision(10) << c2 << endl;
			complex<double> c = pow(c1, k)*conj(c2);
			cout << "k: " << k << ", C: " << c << endl;
			if (kind == 0) features[i] = real(c);
			else features[i] = imag(c);
		}
		delete[] M_p_Local;

		return make_tuple(featuresCount, features);
	}

	/// <summary>Ekstrachuje cechy z podanych plikow</summary>
	/// <param name = 'paths'>Wektor sciezek do plikow</param>
	virtual tuple<int, int, const double* const*> extractMultipleFeatures(const vector<string> &paths)  override
	{
		const double ** X = new const double*[paths.size()];

#pragma omp parallel for num_threads(OMP_NUM_THR)
		for (int i = 0; i < (int)paths.size(); i++)
		{
			ZernikeFPIIinvariants zernike(this);

			auto[fc, fets] = zernike.extractFeatures(paths[i]);
			X[i] = fets;
		}
		return make_tuple((int)paths.size(), featuresCount, X);
	}
};

Extractor* InitializeExtractor(string extName, int *parameters, SaveFileType fileType = SaveFileType::binary64bit)
{
	if (extName == HaarExtractor::GetType())
		return new HaarExtractor(parameters[1], parameters[2], parameters[0], fileType);
	else if (extName == HOGExtractor::GetType())
		return new HOGExtractor(parameters[0], parameters[1], parameters[2], fileType);
	else if (extName == PFMM::GetType())
		return new PFMM(parameters[0], parameters[1], parameters[2], parameters[3], fileType);
	else if (extName == ZernikeII::GetType())
		return new ZernikeII(parameters[0], parameters[1], parameters[2], parameters[3], fileType);
	else if (extName == ZernikePII::GetType())
		return new ZernikePII(parameters[0], parameters[1], parameters[2], parameters[3], parameters[4], fileType);
	else if (extName == ZernikeZII::GetType())
		return new ZernikeZII(parameters[0], parameters[1], parameters[2], parameters[3], parameters[4], parameters[5], fileType);
	else if (extName == ZernikeIIinvariants::GetType())
		return new ZernikeIIinvariants(parameters[0], parameters[1], parameters[2], parameters[3], parameters[4], fileType);
	else if (extName == ZernikeFPII::GetType())
		return new ZernikeFPII(parameters[0], parameters[1], parameters[2], parameters[3], parameters[4], fileType);
	else if (extName == ZernikeFPIIinvariants::GetType())
		return new ZernikeFPIIinvariants(parameters[0], parameters[1], parameters[2], parameters[3], parameters[4], fileType);
	else
		return nullptr;
}