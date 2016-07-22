#include "stdafx.h"
#include "ProPinballBridge.h"
#include "boost/interprocess/ipc/message_queue.hpp"
#include <stdio.h>

using namespace boost::interprocess;

ProPinballBridge::ProPinballDmd::ProPinballDmd()
{
	const char* QUEUE_NAME = "dmd_dot_matrix_display_data";
	message_queue = open_message_queue(QUEUE_NAME);
	dot_matrix_data_message_buffer = new unsigned char[128 * 32];
}

void ProPinballBridge::ProPinballDmd::Release()
{
	delete this;
}

void ProPinballBridge::ProPinballDmd::GetFrames(DmdFrameReceived^ callback)
{
	if (message_queue)
	{
		bool received_message = false;
		do
		{
			try
			{
				unsigned int priority;
				message_queue::size_type received_size;

				const boost::posix_time::ptime wait_time = microsec_clock::universal_time() + boost::posix_time::milliseconds(500);

				printf("+++ Fetching frame from queue...\n");
				received_message = message_queue->timed_receive(dot_matrix_data_message_buffer,
					128 * 32,
					received_size,
					priority,
					wait_time);

				if (received_message)
				{
					if (received_size == 128 * 32)
					{
						printf("+++ Message received, calling delegate.\n");
						callback(dot_matrix_data_message_buffer);
					}
					else
					{
						printf("+++ Received dmd data message size %d, but expecting size %d\n", (int)received_size, 128*32);
					}
				} 
				else
				{
					printf("+++ No message received.\n");
				}

			}
			catch (interprocess_exception &exception)
			{
				printf("+++ Error receiving dot matrix display data message: '%s'\n", exception.what());
			}
		} while (received_message);
	} 
	else
	{
		printf("+++ Message queue invalid, aborting.\n");
	}
}

boost::interprocess::message_queue* ProPinballBridge::ProPinballDmd::open_message_queue(const std::string& message_queue_name)
{
	boost::interprocess::message_queue* message_queue = nullptr;

	try
	{
		message_queue = new boost::interprocess::message_queue(open_only, message_queue_name.c_str());
		printf("+++ Message queue opened.\n");
	}
	catch (interprocess_exception &exception)
	{
		printf("+++ Failed to open message queue '%s', error: '%s'\n", message_queue_name.c_str(), exception.what());
		exit(1);
	}
	return message_queue;
}