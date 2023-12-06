﻿//
// Aurio: Audio Processing, Analysis and Retrieval Library
// Copyright (C) 2010-2017  Mario Guggenberger <mg@protyposis.net>
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
//
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.
//

using System;
using System.Runtime.InteropServices;

namespace Aurio.PFFFT
{
    public unsafe class PFFFT : IDisposable
    {
        private int size;
        private IntPtr setup;
        private IntPtr alignedBuffer1;
        private IntPtr alignedBuffer2;

        public PFFFT(int size, Transform transform)
        {
            if ((size * 4) % 16 != 0)
            {
                // For more info see pffft.h
                throw new Exception("invalid size, must be aligned to a 16-byte boundary");
            }

            this.size = size;
            setup = InteropWrapper.pffft_new_setup(size, transform);

            if (size >= 16384)
            {
                Console.WriteLine("WARNING: size too large, might result in low performance");
                // TODO if this ever gets a problem, implement a "work" area, see pffft.h @ pffft_transform
            }

            uint bufferByteSize = (uint)size * 4;
            alignedBuffer1 = InteropWrapper.pffft_aligned_malloc(new UIntPtr(bufferByteSize));
            alignedBuffer2 = InteropWrapper.pffft_aligned_malloc(new UIntPtr(bufferByteSize));
        }

        public int Size
        {
            get { return size; }
        }

        private void Transform(float* input, float* output, Direction direction)
        {
            // The non-ordered transform pffft_transform may be faster,
            // but all Aurio algorithms expect the canonical ordered form
            InteropWrapper.pffft_transform_ordered(setup, input, output, null, direction);

            // Scale backward transform by 1/N (according to docs)
            if (direction == Direction.Backward)
            {
                for (int x = 0; x < size; x++)
                {
                    output[x] /= size;
                }
            }
        }

        private void CheckSize(float[] array)
        {
            if (array.Length != size)
            {
                throw new Exception(
                    "invalid size (expected " + size + ", given " + array.Length + ")"
                );
            }
        }

        public void Forward(float[] inPlaceBuffer)
        {
            CheckSize(inPlaceBuffer);

            Marshal.Copy(inPlaceBuffer, 0, alignedBuffer1, inPlaceBuffer.Length);
            Transform((float*)alignedBuffer1, (float*)alignedBuffer1, Direction.Forward);
            Marshal.Copy(alignedBuffer1, inPlaceBuffer, 0, inPlaceBuffer.Length);
        }

        public void Forward(float[] input, float[] output)
        {
            CheckSize(input);
            CheckSize(output);

            Marshal.Copy(input, 0, alignedBuffer1, input.Length);
            Transform((float*)alignedBuffer1, (float*)alignedBuffer2, Direction.Forward);
            Marshal.Copy(alignedBuffer2, output, 0, output.Length);
        }

        public void Backward(float[] inPlaceBuffer)
        {
            CheckSize(inPlaceBuffer);

            Marshal.Copy(inPlaceBuffer, 0, alignedBuffer1, inPlaceBuffer.Length);
            Transform((float*)alignedBuffer1, (float*)alignedBuffer1, Direction.Backward);
            Marshal.Copy(alignedBuffer1, inPlaceBuffer, 0, inPlaceBuffer.Length);
        }

        public void Backward(float[] input, float[] output)
        {
            CheckSize(input);
            CheckSize(output);

            Marshal.Copy(input, 0, alignedBuffer1, input.Length);
            Transform((float*)alignedBuffer1, (float*)alignedBuffer2, Direction.Backward);
            Marshal.Copy(alignedBuffer2, output, 0, output.Length);
        }

        public static int SimdSize
        {
            get { return InteropWrapper.pffft_simd_size(); }
        }

        public void Dispose()
        {
            if (setup != IntPtr.Zero)
            {
                InteropWrapper.pffft_destroy_setup(setup);
                setup = IntPtr.Zero;
                InteropWrapper.pffft_aligned_free(alignedBuffer1);
                InteropWrapper.pffft_aligned_free(alignedBuffer2);
            }
        }

        ~PFFFT()
        {
            Dispose();
        }
    }
}
