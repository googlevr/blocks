// Copyright 2020 The Blocks Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using UnityEngine;
using System;

namespace TiltBrush {

/// Usage:
///   var nq = new NeuQuant();
///   nq.Learn(image);        // Call one or more times
///   nq.FinishLearning();
///   index = nq.Map(color);  // Call one or more times
///   byte[] map = nq.ColorMap();
public class NeuQuant {
  struct Pixel {
    public int b, g, r;
    /// Palette index
    public int c;
  }

  const int kNetSize = 256; /* number of colours used */
  /* four primes near 500 - assume no image has a length so large */
  /* that it is divisible by all four primes */
  const int kPrime1 = 499;
  const int kPrime2 = 491;
  const int kPrime3 = 487;
  const int kPrime4 = 503;
  const int kMinPixels = kPrime4;


  /* net Definitions
     ------------------- */
  const int kMaxNetPos = (kNetSize - 1);
  const int kNetBiasShift = 4; /* bias for colour values */
  const int kNumCycles = 100; /* no. of learning cycles */

  /* defs for freq and bias */
  const int kIntBiasShift = 16; /* bias for fractions */
  const int kIntBias = (((int)1) << kIntBiasShift);
  const int kGammaShift = 10; /* gamma = 1024 */
  const int kGamma = (((int)1) << kGammaShift);
  const int kBetaShift = 10;
  const int kBeta = (kIntBias >> kBetaShift); /* kBeta = 1/1024 */
  const int kBetaGamma = (kIntBias << (kGammaShift - kBetaShift));

  /* defs for decreasing radius factor */
  const int kInitRad = (kNetSize >> 3); /* for 256 cols, radius starts */
  const int kRadiusBiasShift = 6; /* at 32.0 biased by 6 bits */
  const int kRadiusBias = (((int)1) << kRadiusBiasShift);
  const int kInitRadius = (kInitRad * kRadiusBias); /* and decreases by a */
  const int kRadiuscDec = 30; /* factor of 1/30 each cycle */

  /* defs for decreasing alpha factor */
  const int kAlphaBiasShift = 10; /* alpha starts at 1.0 */
  const int kInitAlpha = (((int)1) << kAlphaBiasShift);

  /* kRadBias and kAlphaRadBias used for m_radpower calculation */
  const int kRadBiasShift = 8;
  const int kRadBias = (((int)1) << kRadBiasShift);
  const int kAlphaRadBShift = (kAlphaBiasShift + kRadBiasShift);
  const int kAlphaRadBias = (((int)1) << kAlphaRadBShift);

  // Preallocate for avoiding garbage
  int[] m_radPowerBuffer = new int[kInitRad];

  Pixel[] m_net = new Pixel[kNetSize];

  // For each green level, returns the index of the Pixel closest to that green.
  // Pixels will be sorted by green
  int[] m_netindex = new int[256];

  /* bias and freq arrays for learning */
  int[] m_bias = new int[kNetSize];
  int[] m_freq = new int[kNetSize];

  bool m_learning;

  public bool IsLearning {
    get { return m_learning; }
  }

  /* Initialise m_net in range (0,0,0) to (255,255,255) and set parameters
     ----------------------------------------------------------------------- */
  public NeuQuant() {
    int i;

    for (i = 0; i < kNetSize; i++) {
      int val = (i << (kNetBiasShift + 8)) / kNetSize;
      m_net[i] = new Pixel { b=val, g=val, r=val, c=-1 };

      m_freq[i] = kIntBias / kNetSize; /* 1/kNetSize */
      m_bias[i] = 0;
    }
    m_learning = true;
  }

  /// Transition from analyzing data to performing color map lookup.
  /// After this, Learn() may not be called.
  /// After this, Map() may be called.
  public void FinishLearning() {
    Debug.Assert(m_learning);
    UnbiasNet();
    BuildIndex();
    m_learning = false;
  }

  /// Create and return a color map, in "RGB" order the way Gif wants it
  public byte[] ColorMap() {
    if (m_learning) {
      FinishLearning();
    }

    int[] index = new int[kNetSize];
    for (int i = 0; i < kNetSize; i++) {
      index[m_net[i].c] = i;
    }

    int k = 0;
    byte[] map = new byte[3 * kNetSize];
    for (int i = 0; i < kNetSize; i++) {
      int j = index[i];
      map[k++] = (byte)(m_net[j].r);
      map[k++] = (byte)(m_net[j].g);
      map[k++] = (byte)(m_net[j].b);
    }

    return map;
  }

  /* Unbias m_net to give byte values 0..255 and record position i to prepare for sort
     ----------------------------------------------------------------------------------- */
  void UnbiasNet() {
    for (int i = 0; i < kNetSize; i++) {
      m_net[i].b >>= kNetBiasShift;
      m_net[i].g >>= kNetBiasShift;
      m_net[i].r >>= kNetBiasShift;
      m_net[i].c = i; /* record colour no */
    }
  }

  /* Insertion sort of m_net and building of m_netindex[0..255] (to do after unbias)
     ------------------------------------------------------------------------------- */
  void BuildIndex() {
    int previouscol = 0;
    int startpos = 0;

    unsafe {
      fixed (Pixel* aPixel = m_net) {
        for (int i = 0; i < kNetSize; i++) {
          Pixel* p = &aPixel[i];
          int smallpos = i;
          int smallval = p->g; /* index on g */
          /* find smallest in i..kNetSize-1 */
          for (int j = i + 1; j < kNetSize; j++) {
            Pixel* q = &aPixel[j];
            if (q->g < smallval) { /* index on g */
              smallpos = j;
              smallval = q->g; /* index on g */
            }
          }
          /* swap p (i) and q (smallpos) entries */
          if (i != smallpos) {
            Pixel* q = &aPixel[smallpos];
            Pixel oldq = *q;
            *q = *p;
            *p = oldq;
          }
          /* smallval entry is now in position i */
          if (smallval != previouscol) {
            m_netindex[previouscol] = (startpos + i) >> 1;
            for (int j = previouscol + 1; j < smallval; j++) {
              m_netindex[j] = i;
            }
            previouscol = smallval;
            startpos = i;
          }
        }
      }
    }
    m_netindex[previouscol] = (startpos + kMaxNetPos) >> 1;
    for (int j = previouscol + 1; j < 256; j++) {
      m_netindex[j] = kMaxNetPos; /* really 256 */
    }
  }

  /* Main Learning Loop
     ------------------ */

  void radiusAlphaToRad(int radius, int alpha, out int rad, int[] radpower) {
    rad = radius >> kRadiusBiasShift;
    if (rad <= 1) {
      rad = 0;
    }
    for (int i = 0; i < rad; i++) {
      radpower[i] = alpha * (((rad * rad - i * i) * kRadBias) / (rad * rad));
    }
  }

  // Returns number that is coprime to i (might be 1)
  // Tries to return a number near 500 (why? cache effects?)
  static int FindCoprime(int i) {
    if ((i % kPrime1) != 0) { return kPrime1; }
    if ((i % kPrime2) != 0) { return kPrime2; }
    if ((i % kPrime3) != 0) { return kPrime3; }
    if ((i % kPrime4) != 0) { return kPrime4; }
    return 1;
  }

  /// Sample factor is in [1, 30]
  /// Higher values reduce the number of pixels sampled.
  public void Learn(Color32[] pixels, int sampleFactor) {
    Debug.Assert(m_learning);
    int nPixels = pixels.Length;
    sampleFactor = (nPixels < kMinPixels) ? 1 : sampleFactor;
    sampleFactor = Mathf.Clamp(sampleFactor, 1, 30);

    int nSamplePixels = nPixels / sampleFactor;
    int alphaDec = 30 + ((sampleFactor - 1) / 3);
    int pixelsPerCycle = Mathf.Max(1, nSamplePixels / kNumCycles);

    // mutable state
    int alpha = kInitAlpha;
    int radius = kInitRadius;
    int rad;
    int[] radpower = m_radPowerBuffer;
    radiusAlphaToRad(radius, alpha, out rad, radpower);

    int step = (nPixels < kMinPixels) ? 1 : FindCoprime(nPixels);
    for (int i = 0, iPixel = 0; i < nSamplePixels; ) {
      int b = pixels[iPixel].b << kNetBiasShift;
      int g = pixels[iPixel].g << kNetBiasShift;
      int r = pixels[iPixel].r << kNetBiasShift;
      int neuron = Contest(b, g, r);

      AlterSingle(alpha, neuron, b, g, r);
      if (rad != 0) {
        AlterNeighbors(rad, radpower, neuron, b, g, r);
      }

      iPixel = (iPixel + step) % nPixels;
      i++;

      if (i % pixelsPerCycle == 0) {
        alpha -= alpha / alphaDec;
        radius -= radius / kRadiuscDec;
        radiusAlphaToRad(radius, alpha, out rad, radpower);
      }
    }
  }

  /// Convert color value to color index
  public int Map(Color32 c) {
    int b = c.b;
    int g = c.g;
    int r = c.r;

    // TODO: this uses manhattan distance; maybe it should use euclidean?
    // TODO: KD tree instead?

    int dist, a, bestd;
    int best;

    bestd = 1000; /* biggest possible dist is 256 * 3 */
    best = -1;

    int i = m_netindex[g];      // moves forward to end
    int j = i - 1;              // moves backward to beginning

    while ((i < kNetSize) || (j >= 0)) {
      if (i < kNetSize) {
        Pixel p = m_net[i];
        dist = p.g - g; /* inx key */
        if (dist >= bestd) {
          i = kNetSize; /* stop iter */
        } else {
          i++;
          if (dist < 0)
            dist = -dist;
          a = p.b - b;
          if (a < 0)
            a = -a;
          dist += a;
          if (dist < bestd) {
            a = p.r - r;
            if (a < 0)
              a = -a;
            dist += a;
            if (dist < bestd) {
              bestd = dist;
              best = p.c;
            }
          }
        }
      }

      if (j >= 0) {
        Pixel p = m_net[j];
        dist = g - p.g; /* inx key - reverse dif */
        if (dist >= bestd)
          j = -1; /* stop iter */
        else {
          j--;
          if (dist < 0)
            dist = -dist;
          a = p.b - b;
          if (a < 0)
            a = -a;
          dist += a;
          if (dist < bestd) {
            a = p.r - r;
            if (a < 0)
              a = -a;
            dist += a;
            if (dist < bestd) {
              bestd = dist;
              best = p.c;
            }
          }
        }
      }
    }
    return (best);
  }

  /* Move adjacent neurons by precomputed alpha*(1-((i-j)^2/[r]^2)) in m_radpower[|i-j|]
     --------------------------------------------------------------------------------- */
  void AlterNeighbors(int rad, int[] radpower, int i, int b, int g, int r) {

    int j, k, lo, hi, a, m;

    lo = i - rad;
    if (lo < -1)
      lo = -1;
    hi = i + rad;
    if (hi > kNetSize)
      hi = kNetSize;

    j = i + 1;
    k = i - 1;
    m = 1;
    while ((j < hi) || (k > lo)) {
      a = radpower[m++];
      if (j < hi) {
        Pixel p = m_net[j];
        try {
          p.b -= (a * (p.b - b)) / kAlphaRadBias;
          p.g -= (a * (p.g - g)) / kAlphaRadBias;
          p.r -= (a * (p.r - r)) / kAlphaRadBias;
        } catch (Exception e) {
          Debug.Log(e);
        }
        m_net[j++] = p;
      }
      if (k > lo) {
        Pixel p = m_net[k];
        try {
          p.b -= (a * (p.b - b)) / kAlphaRadBias;
          p.g -= (a * (p.g - g)) / kAlphaRadBias;
          p.r -= (a * (p.r - r)) / kAlphaRadBias;
        } catch (Exception e) {
          Debug.Log(e);
        }
        m_net[k--] = p;
      }
    }
  }

  /* Move neuron i towards biased (b,g,r) by factor alpha
     ---------------------------------------------------- */
  protected void AlterSingle(int alpha, int i, int b, int g, int r) {
    /* alter hit neuron */
    Pixel n = m_net[i];
    n.b -= (alpha * (n.b - b)) / kInitAlpha;
    n.g -= (alpha * (n.g - g)) / kInitAlpha;
    n.r -= (alpha * (n.r - r)) / kInitAlpha;
    m_net[i] = n;
  }

  /* Search for biased BGR values
     ---------------------------- */
  protected int Contest(int b, int g, int r) {

    /* finds closest neuron (min dist) and updates freq */
    /* finds best neuron (min dist-bias) and returns position */
    /* for frequently chosen neurons, freq[i] is high and bias[i] is negative */
    /* bias[i] = kGamma*((1/kNetSize)-freq[i]) */

    int i, dist, a, biasdist, betafreq;
    int bestpos, bestbiaspos, bestd, bestbiasd;

    bestd = ~(((int)1) << 31);
    bestbiasd = bestd;
    bestpos = -1;
    bestbiaspos = bestpos;

    for (i = 0; i < kNetSize; i++) {
      Pixel n = m_net[i];
      dist = n.b - b;
      if (dist < 0)
        dist = -dist;
      a = n.g - g;
      if (a < 0)
        a = -a;
      dist += a;
      a = n.r - r;
      if (a < 0)
        a = -a;
      dist += a;
      if (dist < bestd) {
        bestd = dist;
        bestpos = i;
      }
      biasdist = dist - ((m_bias[i]) >> (kIntBiasShift - kNetBiasShift));
      if (biasdist < bestbiasd) {
        bestbiasd = biasdist;
        bestbiaspos = i;
      }
      betafreq = (m_freq[i] >> kBetaShift);
      m_freq[i] -= betafreq;
      m_bias[i] += (betafreq << kGammaShift);
    }
    m_freq[bestpos] += kBeta;
    m_bias[bestpos] -= kBetaGamma;
    return (bestbiaspos);
  }
}
}  // namespace TiltBrush
