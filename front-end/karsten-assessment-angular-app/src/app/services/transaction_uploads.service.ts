import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_URL } from './api.config';

export interface TransactionUpload {
  id: number;
  upload_date: string;
  file_name: string;
  file_size: number;
  incoming_transaction_count: number;
  outgoing_transaction_count: number;
  status: string;
  error_message: string | null;
}

@Injectable({ providedIn: 'root' })
export class TransactionUploadsService {
  constructor(private http: HttpClient) {}

  getTransactionUploads(): Observable<TransactionUpload[]> {
    return this.http.get<TransactionUpload[]>(`${API_URL}/transaction_uploads/get_all`);
  }

  uploadCsv(file: File): Observable<any> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post(`${API_URL}/transaction_uploads/upload_csv`, formData);
  }
}