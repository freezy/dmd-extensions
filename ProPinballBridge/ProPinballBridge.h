#pragma once
#include "boost/interprocess/ipc/message_queue.hpp"

namespace ProPinballBridge
{
	public delegate void DmdFrameReceived(unsigned char* frame);

	public ref class ProPinballDmd
	{
		public:
			ProPinballDmd(void);

			void GetFrames(DmdFrameReceived^ callback);
			void Release();

			int Status;
			const char* Error;

		private:
			boost::interprocess::message_queue* message_queue;
			boost::interprocess::message_queue* open_message_queue(const std::string& message_queue_name);
			unsigned char* dot_matrix_data_message_buffer;
		};
	}

